module Pulumi.FSharp.AzureNative.Components.FunctionAppPackageInternals

open Pulumi.FSharp.NamingConventions.Azure.Region
open Pulumi.FSharp.AzureNative.KeyVault.Inputs
open Pulumi.FSharp.AzureNative.Storage.Inputs
open Pulumi.FSharp.AzureNative.Authorization
open Pulumi.FSharp.AzureNative.Web.Inputs
open Pulumi.FSharp.AzureNative.Insights
open Pulumi.FSharp.AzureNative.KeyVault
open Pulumi.FSharp.AzureNative.Storage
open Pulumi.AzureNative.Authorization
open Pulumi.FSharp.AzureNative.Web
open Pulumi.AzureNative.KeyVault
open Pulumi.AzureNative.Insights
open Pulumi.AzureNative.Storage
open Pulumi.FSharp.AzureNative
open Pulumi.AzureNative.Web
open Pulumi.FSharp.Outputs
open Pulumi.FSharp.Assets
open Pulumi

type FunctionAppPackageResources =
    {
        Storage   : StorageAccount
        Container : BlobContainer
        Blob      : Blob
        Insight   : Component
        Plan      : AppServicePlan
        App       : WebApp
    }

let create workload (resourceGroupName : Input<string>) functionAppPublishPath =
    let stack =
        Deployment.Instance.StackName
    
    let storage =
        storageAccount {
            name          $"sa{workload}{stack}{shortName}001"
            resourceGroup resourceGroupName
            sku           { name SkuName.Standard_LRS }
            kind          Kind.StorageV2
        }
        
    let functionPlan =
        appServicePlan {
            name          $"asp-{workload}-{stack}-{shortName}-001"
            resourceGroup resourceGroupName            
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
            resourceGroup resourceGroupName
            name          "deployment"
            
            PublicAccess.None
        }
        
    let applicationBlob =
        blob {
            name          "application.zip"
            containerName container.Name
            resourceGroup resourceGroupName
            accountName   storage.Name
            source        { ArchivePath = functionAppPublishPath }.ToPulumiType
            
            BlobType.Block
        }

    let appInsights =
        ``component`` {
            name            $"ai-{workload}-{stack}-{shortName}-001"
            resourceGroup   resourceGroupName
            kind            "web"
        }
        
    let storageConnectionString =
        secretOutput {
            let! groupName =
                resourceGroupName.ToOutput()

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

    let subscriptionId =
        output {
            let! result =
                GetClientConfig.InvokeAsync()
                
            return result.SubscriptionId
        }
        
    let deploymentPrincipalId =
        output {
            let! result =
                GetClientConfig.InvokeAsync()
                
            return result.ObjectId
        }

    let keyVault =
        vault {
            name          $"kv{workload}{stack}{shortName}001"
            resourceGroup resourceGroupName
            
            vaultProperties {
                enableRbacAuthorization true
                tenantId                currentTenantId
                
                KeyVault.Inputs.sku {
                    family SkuFamily.A                    
                    SkuName.Standard                    
                }
            }
        }

    // Can we use MI?
    let csSecret =
        secret {
            vaultName     keyVault.Name
            resourceGroup resourceGroupName
            name          "storageConnectionString"
            
            secretProperties {
                value storageConnectionString
            }
        }
        
    // Can we use MI?
    let instrumentationKeySecret =
        secret {
            vaultName     keyVault.Name
            resourceGroup resourceGroupName
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
            name               $"func-{workload}-{stack}-{shortName}-001"
            resourceGroup      resourceGroupName
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
                    nameValuePair { name "WEBSITE_RUN_FROM_PACKAGE"      ; value applicationBlob.Url                         }
                    nameValuePair { name "APPINSIGHTS_INSTRUMENTATIONKEY"; value (kvReference instrumentationKeySecret.Name) }
                ]
            }
        }
    
    let ``Storage Blob Data Reader`` =
        "2a2b9908-6ea1-4ae2-8e65-a410df84e7d1"
    
    roleAssignment {
        name             "function-to-container-read"
        principalId      (webApp.Identity.Apply(fun wai -> wai.PrincipalId))
        principalType    PrincipalType.ServicePrincipal
        roleDefinitionId (Output.Format($"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{``Storage Blob Data Reader``}"))
        scope            container.Id
    }
    
    let ``Key Vault Administrator`` =
        "00482a5a-887f-4fb3-b363-3b7fe8e74483"
    
    roleAssignment {
        name             "deployment-to-keyvault-admin"
        principalId      deploymentPrincipalId
        roleDefinitionId (Output.Format($"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{``Key Vault Administrator``}"))
        scope            keyVault.Id
    }
    
    let ``Key Vault Secrets User`` = "4633458b-17de-408a-b874-0445c86b69e6"
    
    let _ =
        output {
            let! appIdentity =
                webApp.Identity
            
            let! csSecredId =
                csSecret.Id
                
            let! instrumentationKeySecretId =
                instrumentationKeySecret.Id
                
            let! assignmentCs =
                roleAssignment {
                    name               "functionAppReadsKeyVaultCs"
                    principalId        appIdentity.PrincipalId
                    roleAssignmentName "920ef309-2f1c-4c03-afa4-3cbca37e5bb3" // (System.Guid.NewGuid().ToString()) cached
                    roleDefinitionId   $"/{csSecredId}/providers/Microsoft.Authorization/roleDefinitions/{``Key Vault Secrets User``}"
                    scope              csSecredId
                    principalType      PrincipalType.ServicePrincipal
                }
                |> fun a -> a.Id
                
            let! assignmentIk =
                roleAssignment {
                    name               "functionAppReadsKeyVaultIk"
                    principalId        appIdentity.PrincipalId
                    roleAssignmentName "920ef309-2f1c-4c03-afa4-3cbca37e5bb5" // (System.Guid.NewGuid().ToString()) cached
                    roleDefinitionId   $"/{instrumentationKeySecretId}/providers/Microsoft.Authorization/roleDefinitions/{``Key Vault Secrets User``}"
                    scope              instrumentationKeySecretId
                    principalType      PrincipalType.ServicePrincipal
                }
                |> fun a -> a.Id
                
            return $"{assignmentCs.[0]}{assignmentIk.[0]}"
        }
    
    {
        Storage   = storage
        Container = container
        Blob      = applicationBlob
        Insight   = appInsights
        Plan      = functionPlan
        App       = webApp
    }