﻿module Program

open Pulumi.FSharp.AzureNative.DevTestLab.Inputs
open Pulumi.FSharp.AzureNative.Compute.Inputs
open Pulumi.FSharp.AzureNative.Network.Inputs
open Pulumi.FSharp.AzureNative.DevTestLab
open Pulumi.FSharp.AzureNative.Resources
open Pulumi.FSharp.AzureNative.Network
open Pulumi.FSharp.AzureNative.Compute
open Pulumi.FSharp.AzureNative.Storage
open Pulumi.AzureNative.DevTestLab
open Pulumi.AzureNative.Network
open Pulumi.AzureNative.Compute
open Pulumi.AzureNative.Storage
open Pulumi.FSharp.Outputs
open Pulumi.FSharp.Config
open Pulumi.FSharp.Assets
open System.Text.Json
open Pulumi.FSharp
open DevelopmentVM
open FSharp.Data
open System.IO
open Pulumi
open System

let nicSubnet = AzureNative.Network.Inputs.subnet
let networkProfile = AzureNative.Compute.Inputs.networkProfile
let pipConfig = AzureNative.Network.Inputs.publicIPAddress
let securityRule = AzureNative.Network.Inputs.securityRule
let subnetNsg = AzureNative.Network.Inputs.networkSecurityGroup
let storageSku = AzureNative.Storage.Inputs.sku

Deployment.run (fun () ->
    let rg =
        resourceGroup {
            name $"rg-dev-{Deployment.Instance.StackName}-{Region.short}-001"
        }

    let vnet =
        virtualNetwork {
            name          $"vnet-dev-{Deployment.Instance.StackName}-{Region.short}-001"
            resourceGroup rg.Name            
            addressSpace  { addressPrefixes "10.0.1.0/24" }
        }
        
    let nsg =
        networkSecurityGroup {
            name          $"nsg-dev-{Deployment.Instance.StackName}-{Region.short}-001"
            resourceGroup rg.Name

            securityRules [
                securityRule {
                    name                     "AllowRdp"
                    destinationAddressPrefix "VirtualNetwork"
                    destinationPortRange     "3389"
                    sourceAddressPrefix      (Http.RequestString("https://api.ipify.org"))
                    sourcePortRange          "*"
                    priority                 1000
                    access                   SecurityRuleAccess.Allow
                    direction                SecurityRuleDirection.Inbound
                    protocol                 SecurityRuleProtocol.Tcp
                }
                
                securityRule {
                    name                     "DenyAll"
                    destinationAddressPrefix "*"
                    destinationPortRange     "*"
                    sourceAddressPrefix      "*"
                    sourcePortRange          "*"
                    priority                 2000
                    access                   SecurityRuleAccess.Deny
                    direction                SecurityRuleDirection.Inbound
                    protocol                 SecurityRuleProtocol.Asterisk
                }
            ]
        }

    let snet =
        subnet {
            name               "VmSubnet"
            resourceGroup      rg.Name
            virtualNetworkName vnet.Name
            addressPrefix      "10.0.1.0/24"
            subnetNsg          { id nsg.Id }
        }

    let pip =
        publicIPAddress {
            name                     $"pip-dev-{Deployment.Instance.StackName}-{Region.short}-001"
            publicIPAllocationMethod IPAllocationMethod.Static
            resourceGroup            rg.Name
            
            publicIPAddressSku {
                name "Basic"
                tier "Regional"
            }
            
            publicIPAddressDnsSettings {
                domainNameLabel config.["vmDnsLabel"]
            }
        }

    let nic =
        networkInterface {
            name          $"nic-dev-{Deployment.Instance.StackName}-{Region.short}-001"
            resourceGroup rg.Name

            ipConfigurations [                
                networkInterfaceIPConfiguration {
                    name                      "ipconfig"
                    privateIPAllocationMethod "Dynamic"                    
                    nicSubnet                 { id snet.Id }
                    pipConfig                 { id pip.Id }
                }
            ]
        }
    
    let vm =
        virtualMachine {
            name          $"vm-dev-{Deployment.Instance.StackName}-{Region.short}-001"
            resourceGroup rg.Name
            licenseType   "Windows_Client"
            
            hardwareProfile {
                vmSize "Standard_D16ds_v4"
            }
            
            networkProfile {
                networkInterfaces [
                    networkInterfaceReference { id nic.Id }
                ]
            }
            
            oSProfile {
                computerName  $"dev{Deployment.Instance.StackName}{Region.short}001"
                adminUsername config.["vmUser"]
                adminPassword secret.["vmPass"]
            }
            
            storageProfile {
                oSDisk {
                    name                  $"osdisk-dev-{Deployment.Instance.StackName}-{Region.short}-001"
                    createOption          DiskCreateOptionTypes.FromImage
                    managedDiskParameters { storageAccountType StorageAccountTypes.Premium_LRS }
                    diskSizeGB            127
                    
                    CachingTypes.ReadWrite
                }
                
                imageReference {
                    offer     "visualstudio2019latest"
                    publisher "microsoftvisualstudio"
                    sku       "vs-2019-ent-latest-win10-n"
                    version   "latest"
                }
            }
        }
    
    globalSchedule {
        name             $"sch-dev-{Deployment.Instance.StackName}-{Region.short}-001"
        resourceName     (Output.Format($"shutdown-computevm-{vm.Name}"))
        resourceGroup    rg.Name
        targetResourceId vm.Id
        taskType         "ComputeVmShutdownTask"
        timeZoneId       "UTC"
        dayDetails       { time "1900" }
        status           EnableStatus.Enabled
        
        notificationSettings {
            emailRecipient config.["vmShutdownNotifyEmail"]
            status         EnableStatus.Enabled
            timeInMinutes  30
        }
    }

    let sa =
        storageAccount {
            name          $"sadev{Deployment.Instance.StackName}{Region.short}001"
            resourceGroup rg.Name
            kind          Kind.StorageV2
            
            storageSku {
                name SkuName.Standard_LRS
            }
        }
        
    let container =
        blobContainer {
            name          "sco-powershell"
            accountName   sa.Name
            resourceGroup rg.Name            
        }
    
    let scriptBlobUrl =
        output {
            let! storageKey =
                secret.["storageKey"]
        
            let script =
                File.ReadAllText("CustomScript.ps1")
                    .Replace("<storageAccount>", config.["storageAccount"])
                    .Replace("<storageKey>", storageKey)
                    .Replace("<shareName>", config.["shareName"])
        
            let blob =
                blob {
                    name          "sbl-customscript"
                    accountName   sa.Name
                    containerName container.Name
                    resourceGroup rg.Name
                    blobName      "CustomScript.ps1"
                    source        { Text = script }.ToPulumiType
                    
                    BlobType.Block
                    BlobAccessTier.Hot
                }
            
            return! blob.Url
        }

    
    let sas =
        output {
            let! accountName =
                sa.Name
        
            let! resourceGroupName =
                rg.Name
                    
            let! blobUrl =
                scriptBlobUrl
                
            let! result =
                ListStorageAccountServiceSASArgs(
                    AccountName            = accountName,
                    Protocols              = HttpProtocol.Https,
                    Permissions            = Union.FromT1 Permissions.R,
                    ResourceGroupName      = resourceGroupName,
                    CanonicalizedResource  = $"/blob/{accountName}{Uri(blobUrl).AbsolutePath}",
                    Resource               = Union.FromT1 SignedResource.B,
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(2.).ToString("O")
                    ) |>
                ListStorageAccountServiceSAS.InvokeAsync
                
            return blobUrl + "?" + result.ServiceSasToken
        }

    let extSettings =
        secretOutput {
            let! fileUrl =
                sas
            
            let value =
                {|
                    fileUris         = [ fileUrl ]
                    commandToExecute = "powershell -ExecutionPolicy Unrestricted -File CustomScript.ps1"
                |} |>
                JsonSerializer.Serialize
            
            return value
        } |>
        InputJson.op_Implicit
    
    virtualMachineExtension {
        name               $"vmext-dev-{Deployment.Instance.StackName}-{Region.short}-001"
        resourceGroup      rg.Name
        vmName             vm.Name
        vmExtensionName    "CustomScriptExtension"
        resourceType       "CustomScriptExtension"
        publisher          "Microsoft.Compute"
        typeHandlerVersion "1.10"
        protectedSettings  extSettings
    }

    dict [ "FQDN"    , pip.DnsSettings.Apply(fun x -> x.Fqdn) :> obj
           "PublicIP", pip.IpAddress                          :> obj ]
)