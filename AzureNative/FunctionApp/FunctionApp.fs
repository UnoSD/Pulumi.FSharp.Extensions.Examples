module Pulumi.FSharp.AzureNative.Components.FunctionApp

open Pulumi.AzureNative.Authorization
open Pulumi.FSharp.AzureNative.KeyVault.Inputs
open Pulumi.FSharp.AzureNative.Storage.Inputs
open Pulumi.FSharp.AzureNative.Authorization
open Pulumi.FSharp.AzureNative.Web.Inputs
open Pulumi.FSharp.AzureNative.Insights
open Pulumi.FSharp.AzureNative.KeyVault
open Pulumi.FSharp.AzureNative.Storage
open Pulumi.FSharp.AzureNative.Web
open Pulumi.AzureNative.KeyVault
open Pulumi.AzureNative.Insights
open Pulumi.AzureNative.Storage
open Pulumi.FSharp.AzureNative
open Pulumi.AzureNative.Web
open Pulumi.FSharp.Outputs
open Pulumi.FSharp.Assets
open Pulumi

type FunctionAppResources =
    {
        Storage   : StorageAccount
        Container : BlobContainer
        Blob      : Blob
        Insight   : Component
        Plan      : AppServicePlan
        App       : WebApp
        RID       : Output<string>
    }

let private kvSku = KeyVault.Inputs.sku

let create resourcesSuffix
           (resourceGroupNameOutput : Input<string>)
           functionAppPublishPath =
    let storage =
        storageAccount {
            name          $"sa{resourcesSuffix}"
            resourceGroup resourceGroupNameOutput
            sku           { name SkuName.Standard_LRS }
            kind          Kind.StorageV2
        }
        
    let functionPlan =
        appServicePlan {
            name          $"asp-{resourcesSuffix}"
            resourceGroup resourceGroupNameOutput            
            kind          "Linux"
            reserved      true
            
            skuDescription {
                name "Y1"
                tier "Dynamic"
            }
        }
        
    let container =
        blobContainer {
            accountName   storage.Name
            resourceGroup resourceGroupNameOutput
            name          "deployment"
            
            PublicAccess.None
        }
        
    let applicationBlob =
        blob {
            name          "application.zip"
            containerName container.Name
            resourceGroup resourceGroupNameOutput
            accountName   storage.Name
            source        { ArchivePath = functionAppPublishPath }.ToPulumiType
            
            BlobType.Block
        }
    
    // Managed identity to get package is not yet supported (? check!)
    let codeBlobUrl =
        secretOutput {
            let! accountName =
                storage.Name
            
            let! groupName =
                resourceGroupNameOutput.ToOutput()
        
            let! containerName =
                container.Name

            let! blobName =
                applicationBlob.Name

            let! result =
                ListStorageAccountServiceSASArgs(
                    AccountName            = accountName,
                    Protocols              = HttpProtocol.Https,
                    SharedAccessStartTime  = "2022-01-01",
                    SharedAccessExpiryTime = "2022-12-30",
                    Resource               = Union.FromT1 SignedResource.B,
                    ResourceGroupName      = groupName,
                    Permissions            = Union.FromT1 Permissions.R,
                    CanonicalizedResource  = $"/blob/{accountName}/{containerName}/{blobName}")
                |> ListStorageAccountServiceSAS.InvokeAsync
            
            let! blobUrl =
                applicationBlob.Url
                
            return $"{blobUrl}?{result.ServiceSasToken}"
        }

    let appInsights =
        ``component`` {
            name            $"ai-{resourcesSuffix}"
            resourceGroup   resourceGroupNameOutput
            kind            "web"
        }
        
    let storageConnectionString =
        secretOutput {
            let! groupName =
                resourceGroupNameOutput.ToOutput()

            let! accountName =
                storage.Name

            let! result =
                ListStorageAccountKeysArgs(
                    ResourceGroupName = groupName,
                    AccountName       = accountName
                )
                |> ListStorageAccountKeys.InvokeAsync

            let key =
                result.Keys
                |> (fun ks -> ks |> Seq.head)
                |> fun k -> k.Value

            return $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={key}"
        }

    let currentTenantId =
        output {
            let! result =
                GetClientConfig.InvokeAsync()
                
            return result.TenantId
        }

    let keyVault =
        vault {
            name          $"kv-{resourcesSuffix}"
            resourceGroup resourceGroupNameOutput
            
            vaultProperties {
                enableRbacAuthorization true
                tenantId                currentTenantId
                kvSku {
                    family SkuFamily.A                    
                    SkuName.Standard                    
                }
            }
        }

    let csSecret =
        secret {
            vaultName     keyVault.Name
            resourceGroup resourceGroupNameOutput
            name          "storageConnectionString"
            
            secretProperties {
                value storageConnectionString
            }
        }
    
    let blobUrlSecret =
        secret {
            vaultName     keyVault.Name
            resourceGroup resourceGroupNameOutput
            name          "applicationBlobSasUrl"
            
            secretProperties {
                value codeBlobUrl
            }
        }
        
    let instrumentationKeySecret =
        secret {
            vaultName     keyVault.Name
            resourceGroup resourceGroupNameOutput
            name          "instrumentationKey"
            
            secretProperties {
                value appInsights.InstrumentationKey
            }
        }
    
    let kvReference (secretNameOutput : Output<string>) =
        output {
            let! vaultName =
                keyVault.Name
                
            let! secretName =
                secretNameOutput
                
            return $"@Microsoft.KeyVault(VaultName={vaultName};SecretName={secretName})"
        }
    
    let webApp =
        webApp {
            name               $"app-{resourcesSuffix}"
            resourceGroup      resourceGroupNameOutput
            serverFarmId       functionPlan.Id
            kind               "FunctionApp"
            
            managedServiceIdentity {
                ManagedServiceIdentityType.SystemAssigned
            }
            
            siteConfig {
                appSettings     [
                    nameValuePair { name "AzureWebJobsStorage"           ; value (kvReference csSecret.Name)                 }
                    nameValuePair { name "runtime"                       ; value "dotnet"                                    }
                    nameValuePair { name "FUNCTIONS_EXTENSION_VERSION"   ; value "~3"                                        }
                    nameValuePair { name "WEBSITE_RUN_FROM_PACKAGE"      ; value (kvReference blobUrlSecret.Name)            }
                    nameValuePair { name "APPINSIGHTS_INSTRUMENTATIONKEY"; value (kvReference instrumentationKeySecret.Name) }
                ]
            }
        }
    
    let ``Key Vault Secrets User`` = "4633458b-17de-408a-b874-0445c86b69e6"
    
    let assignmentId =
        output {
            let! appIdentity =
                webApp.Identity
            
            let! csSecredId =
                csSecret.Id
                
            let! blobUrlSecretId =
                blobUrlSecret.Id

            let! instrumentationKeySecretId =
                instrumentationKeySecret.Id
                
            let! assignment1 =
                roleAssignment {
                    name               "functionAppReadsKeyVaultCs"
                    principalId        appIdentity.PrincipalId
                    roleAssignmentName "920ef309-2f1c-4c03-afa4-3cbca37e5bb3" // (System.Guid.NewGuid().ToString()) cached
                    roleDefinitionId   $"/{csSecredId}/providers/Microsoft.Authorization/roleDefinitions/{``Key Vault Secrets User``}"
                    scope              csSecredId
                    principalType      PrincipalType.ServicePrincipal
                }
                |> fun a -> a.Id
                
            let! assignment2 =
                roleAssignment {
                    name               "functionAppReadsKeyVaultBu"
                    principalId        appIdentity.PrincipalId
                    roleAssignmentName "920ef309-2f1c-4c03-afa4-3cbca37e5bb4" // (System.Guid.NewGuid().ToString()) cached
                    roleDefinitionId   $"/{blobUrlSecretId}/providers/Microsoft.Authorization/roleDefinitions/{``Key Vault Secrets User``}"
                    scope              blobUrlSecretId
                    principalType      PrincipalType.ServicePrincipal
                }
                |> fun a -> a.Id
                
            let! assignment3 =
                roleAssignment {
                    name               "functionAppReadsKeyVaultIk"
                    principalId        appIdentity.PrincipalId
                    roleAssignmentName "920ef309-2f1c-4c03-afa4-3cbca37e5bb5" // (System.Guid.NewGuid().ToString()) cached
                    roleDefinitionId   $"/{instrumentationKeySecretId}/providers/Microsoft.Authorization/roleDefinitions/{``Key Vault Secrets User``}"
                    scope              instrumentationKeySecretId
                    principalType      PrincipalType.ServicePrincipal
                }
                |> fun a -> a.Id
                
            return $"{assignment1.[0]}{assignment2.[0]}{assignment3.[0]}"
        }
    
    {
        Storage   = storage
        Container = container
        Blob      = applicationBlob
        Insight   = appInsights
        Plan      = functionPlan
        App       = webApp
        RID       = assignmentId
    }