module KubernetesCluster

open Pulumi.FSharp.AzureNative.ContainerService.Inputs
open Pulumi.FSharp.AzureNative.ContainerService
open Pulumi.AzureNative.Resources
open Pulumi

type ResourceIdentityType = Pulumi.AzureNative.ContainerService.ResourceIdentityType

let create (lawId : string)
           (kubeletIdentityId : string)
           (kubeletClientId : string)
           (kubeletObjectId : string)
           (loadBalancerPublicIpId : string)
           (rg : ResourceGroup) =
    managedCluster {
        name                 $"aks-ohc-{Deployment.Instance.StackName}-weu-001"
        resourceGroup        rg.Name
        dnsPrefix            "aks-ohc-dns"
        enableRBAC           true
        kubernetesVersion    "1.18.14"
        nodeResourceGroup    "MC_ohc_aks-ohc_westeurope"

        addonProfiles [
            "KubeDashboard"         , managedClusterAddonProfile { enabled false }
            "azurePolicy"           , managedClusterAddonProfile { enabled false }
            "httpApplicationRouting", managedClusterAddonProfile { enabled false }

            "omsAgent", managedClusterAddonProfile { 
                enabled true
                config [
                    "logAnalyticsWorkspaceResourceID", lawId
                ]
            }
        ]

        agentPoolProfiles [
            managedClusterAgentPoolProfile {
                count               3
                maxPods             110
                mode                "System"
                name                "agentpool"
                orchestratorVersion "1.18.14"
                osDiskSizeGB        128
                osDiskType          "Managed"
                osType              "Linux"
                resourceType        "VirtualMachineScaleSets"
                vmSize              "Standard_DS2_v2"
            }
        ]

        identityProfile [
            "kubeletidentity", managedClusterPropertiesIdentityProfile { 
                clientId   kubeletClientId
                objectId   kubeletObjectId
                resourceId kubeletIdentityId
            }
        ]  

        managedClusterAPIServerAccessProfile  { enablePrivateCluster false }
        managedClusterIdentity                { ResourceIdentityType.SystemAssigned }
        managedClusterServicePrincipalProfile { clientId "msi" }      

        containerServiceNetworkProfile {
            dnsServiceIP     "10.0.0.10"
            dockerBridgeCidr "172.17.0.1/16"
            loadBalancerSku  "Standard"
            networkPlugin    "kubenet"
            outboundType     "loadBalancer"
            podCidr          "10.244.0.0/16"
            serviceCidr      "10.0.0.0/16"

            managedClusterLoadBalancerProfile {
                effectiveOutboundIPs [
                    resourceReference {
                        id loadBalancerPublicIpId
                    }
                ]

                allocatedOutboundPorts 0
                idleTimeoutInMinutes   30

                managedClusterLoadBalancerProfileManagedOutboundIPs { count 1 }
            }
        }
        
        managedClusterSKU {
            name "Basic"
            tier "Free"
        }
    }