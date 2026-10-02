module Program

open Pulumi.FSharp.AzureNative.Network.Inputs
open Pulumi.FSharp.AzureNative.Authorization
open Pulumi.FSharp.NamingConventions.Azure
open Pulumi.FSharp.AzureNative.Resources
open Pulumi.FSharp.AzureNative.Network
open Pulumi.AzureNative.Network
open Pulumi.FSharp.Tls.Inputs
open Pulumi.FSharp.AzureAD
open Pulumi.FSharp.Random
open Pulumi.FSharp.Tls
open Pulumi.FSharp
open Pulumi
open VPN

Deployment.run (fun () ->    
    let individualCaPrivateKey =
        privateKey {
            name      "individual-ca-private-key"
            algorithm "RSA"
        }

    let individualCaCertificate =
        selfSignedCert {
            name                "individual-ca-certificate"
            keyAlgorithm        "RSA"
            privateKeyPem       individualCaPrivateKey.PrivateKeyPem
            isCaCertificate     true
            validityPeriodHours (1095 * 24)

            allowedUses [
                "cert_signing"
                "crl_signing"
            ]

            subjects [
                selfSignedCertSubject {
                    commonName "VPN Individual CA"
                }
            ]
        }

    let rg =
        resourceGroup {
            name $"rg-vpn-{Deployment.Instance.StackName}-{Region.shortName}-001"
        }

    let vnet =
        virtualNetwork {
            name          $"vnet-vpn-{Deployment.Instance.StackName}-{Region.shortName}-001"
            resourceGroup rg.Name            
            addressSpace  { addressPrefixes "10.255.0.0/16" }
        }
        
    let snet =
        subnet {
            name               "GatewaySubnet"
            resourceGroup      rg.Name
            virtualNetworkName vnet.Name
            addressPrefix      "10.255.1.0/24"
        }

    let pip =
        publicIPAddress {
            name                     $"pip-vpn-{Deployment.Instance.StackName}-{Region.shortName}-001"
            publicIPAllocationMethod IPAllocationMethod.Static
            resourceGroup            rg.Name
            
            publicIPAddressSku {
                name "Standard"
            }
        }
    
    let gateway =
        virtualNetworkGateway {
            resourceGroup rg.Name
            name          $"vpng-vpn-{Deployment.Instance.StackName}-{Region.shortName}-001"
            
            ipConfigurations [
                virtualNetworkGatewayIPConfiguration {
                    name "gwipconfig1"
                    
                    publicIPAddress (subResource {
                        id pip.Id
                    })
                    
                    subnet (subResource {
                        id snet.Id
                    })
                }
            ]
            
            virtualNetworkGatewaySku {
                name VirtualNetworkGatewaySkuName.VpnGw1AZ
                tier VirtualNetworkGatewaySkuTier.VpnGw1AZ
            }
            
            gatewayType VirtualNetworkGatewayType.Vpn            
            vpnType     VpnType.RouteBased
            
            vpnClientConfiguration {
                vpnClientProtocols [ 
                    Union.FromT1 VpnClientProtocol.OpenVPN
                ]
                
                vpnClientRootCertificates [
                    vpnClientRootCertificate {
                        name           "P2SIndividualRootCert"
                        publicCertData (individualCaCertificate.CertPem.Apply(X509.removeBeginEndCertificate))
                    }
                ]
                
                addressSpace {
                    addressPrefixes "172.16.201.0/24"
                }
            }
        }

    let config = Config "VPN"

    let siteToSiteOutputs =
        match config.GetBoolean "s2sEnabled" |> Option.ofNullable with
        | Some true ->
            let preSharedKey =
                randomPassword {
                    name    "s2s-shared-key"
                    length  64
                    special false
                }

            let localGateway =
                localNetworkGateway {
                    name             $"lgw-vpn-{Deployment.Instance.StackName}-{Region.shortName}-001"
                    resourceGroup    rg.Name
                    location         rg.Location

                    fqdn             (config.Require "s2sGatewayFqdn")

                    addressSpace {
                        addressPrefixes (config.RequireObject<string array> "s2sAddressPrefixes")
                    }
                }

            virtualNetworkGatewayConnection {
                name               $"con-vpn-{Deployment.Instance.StackName}-{Region.shortName}-001"
                resourceGroup      rg.Name
                location           rg.Location
                connectionType     VirtualNetworkGatewayConnectionType.IPsec
                connectionProtocol VirtualNetworkGatewayConnectionProtocol.IKEv2
                enableBgp          false
                sharedKey          preSharedKey.Result

                // route-based: wildcard 0.0.0.0/0 selectors
                usePolicyBasedTrafficSelectors false

                ipsecPolicies [
                    ipsecPolicy {
                        // IKE / Phase 1  (tool: aes256-sha256-prfsha256-modp2048)
                        ikeEncryption       IkeEncryption.AES256
                        // sets integrity AND prf
                        ikeIntegrity        IkeIntegrity.SHA256
                        // modp2048
                        dhGroup             DhGroup.DHGroup14
                        // IPsec / ESP / Phase 2  (tool: aes256gcm16-modp2048)
                        ipsecEncryption     IpsecEncryption.GCMAES256
                        // GCM => enc & integrity must match
                        ipsecIntegrity      IpsecIntegrity.GCMAES256
                        // modp2048
                        pfsGroup            PfsGroup.PFS2048
                        saLifeTimeSeconds   27000
                        saDataSizeKilobytes 102400000
                    }
                ]

                virtualNetworkGateway1 (Inputs.virtualNetworkGateway {
                    id gateway.Id
                })

                Inputs.localNetworkGateway {
                    id localGateway.Id
                }
            }

            [ "VpnGatewayPublicIp" , pip.IpAddress       :> obj
              "SiteToSiteSharedKey", preSharedKey.Result :> obj ]
        | _ -> []

    let dnsUpdaterOutputs =
        match config.GetBoolean "ddnsEnabled" |> Option.ofNullable with
        | Some true ->
            let dnsGroup             = config.Require "ddnsResourceGroup"
            let dnsZone              = config.Require "ddnsZoneName"
            let dnsRecord            = config.Require "ddnsRecordName"
            let credentialVersion    = config.Get     "ddnsCredentialVersion" |> Option.ofObj |> Option.defaultValue "1"
            let subscriptionOverride = config.Get     "ddnsSubscriptionId" |> Option.ofObj

            let azureContext =
                Output.Create<AzureNative.Authorization.GetClientConfigResult>
                    (AzureNative.Authorization.GetClientConfig.InvokeAsync())
            let directoryContext =
                Output.Create<AzureAD.GetClientConfigResult>(AzureAD.GetClientConfig.InvokeAsync())
            let subscription =
                azureContext.Apply(fun azure -> subscriptionOverride |> Option.defaultValue azure.SubscriptionId)
            let subscriptionScope =
                subscription.Apply(fun id -> $"/subscriptions/{id}")
            let groupScope =
                subscriptionScope.Apply(fun scope -> $"{scope}/resourceGroups/{dnsGroup}")
            let recordScope =
                groupScope.Apply(fun scope -> $"{scope}/providers/Microsoft.Network/dnsZones/{dnsZone}/A/{dnsRecord}")
            let owner =
                directoryContext.Apply(fun directory -> directory.ObjectId)

            let app =
                application {
                    name           "dns-updater-app"
                    displayName    $"dns-updater-{Deployment.Instance.ProjectName}-{Deployment.Instance.StackName}"
                    signInAudience "AzureADMyOrg"
                    owners         [owner]
                }

            let principal =
                servicePrincipal {
                    name          "dns-updater-principal"
                    applicationId app.ApplicationId
                    owners        [owner]
                }

            let credential =
                applicationPassword {
                    name                    "dns-updater-password"
                    applicationObjectId     app.ObjectId
                    displayName             "DNS updater"
                    endDateRelative         "8760h"
                    rotateWhenChanged       ["version", credentialVersion]
                }

            let definitionId =
                randomUuid {
                    name    "dns-updater-role-id"
                    keepers ["subscription", subscription.Apply(fun value -> value :> obj)]
                }

            let role =
                roleDefinition {
                    name             "dns-updater-role"
                    roleDefinitionId definitionId.Result
                    roleName         (definitionId.Result.Apply(fun id -> $"DNS A record updater {id}"))
                    roleType         "CustomRole"
                    description      "Update only the assigned public DNS A record set"
                    scope            subscriptionScope
                    assignableScopes [groupScope]

                    permissions [
                        Inputs.permission {
                            actions [
                                "Microsoft.Network/dnsZones/A/write"
                            ]
                        }
                    ]
                }

            let assignmentId =
                randomUuid {
                    name "dns-updater-assignment-id"
                    keepers [
                        "principal", principal.ObjectId.Apply(fun value -> value :> obj)
                        "role"     , role.Id.Apply(fun value -> value :> obj)
                        "scope"    , recordScope.Apply(fun value -> value :> obj)
                    ]
                }

            roleAssignment {
                name               "dns-updater-assignment"
                roleAssignmentName assignmentId.Result
                scope              recordScope
                principalId        principal.ObjectId
                principalType      AzureNative.Authorization.PrincipalType.ServicePrincipal
                roleDefinitionId   role.Id
            }

            let updaterConfig =
                Output.Tuple(directoryContext, subscription, app.ApplicationId).Apply(fun struct (directory, subscriptionId, clientId) ->
                    dict [
                        "tenantId"      , directory.TenantId
                        "clientId"      , clientId
                        "subscriptionId", subscriptionId
                        "resourceGroup" , dnsGroup
                        "zoneName"      , dnsZone
                        "recordName"    , dnsRecord
                    ]
                    |> fun values -> System.Collections.Generic.Dictionary<string, string> values)

            [ "DnsUpdaterConfig"             , updaterConfig :> obj
              "DnsUpdaterClientSecret"       , credential.Value :> obj
              "DnsUpdaterCredentialExpiresAt", credential.EndDate :> obj ]
        | _ -> []

    let additionalClientNames =
        config.GetObject<string[]>("additionalClients")
        |> Option.ofObj
        |> Option.map List.ofArray
    
    let profileResult = VpnProfile.generate rg gateway

    let defaultKey =
        privateKey {
            name      "private-key-client"
            algorithm "RSA"
        }

    let defaultCert =
        ClientCertificate.create individualCaCertificate individualCaPrivateKey defaultKey "client"

    let clientNameToOvpnFile (client : string) =
        let key =
            privateKey {
                name      $"private-key-{client}"
                algorithm "RSA"
            }
        let cert = ClientCertificate.create individualCaCertificate individualCaPrivateKey key client
        $"OpenVpnFile-{client}", VpnProfile.getOpenVpnConfigurationFile cert key profileResult :> obj

    let additionalOvpnFiles =
        additionalClientNames
        |> Option.map (List.map clientNameToOvpnFile)
        |> Option.defaultValue []

    ( "OpenVpnFile", VpnProfile.getOpenVpnConfigurationFile defaultCert defaultKey profileResult :> obj )
    :: additionalOvpnFiles
    @ siteToSiteOutputs
    @ dnsUpdaterOutputs
    |> dict
)