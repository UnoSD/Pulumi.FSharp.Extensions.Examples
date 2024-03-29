﻿module Program

open Pulumi.FSharp.AzureNative.Network.Inputs
open Pulumi.FSharp.NamingConventions.Azure
open Pulumi.FSharp.AzureNative.Resources
open Pulumi.FSharp.AzureNative.Network
open Pulumi.AzureNative.Network
open Pulumi.FSharp.Tls.Inputs
open Pulumi.FSharp.Tls
open Pulumi.FSharp
open Pulumi
open VPN

Deployment.run (fun () ->    
    let caPrivateKey =
        privateKey {
            name      "ca-private-key"
            algorithm "RSA"
        }

    let caCertificate =
        selfSignedCert {
            name                "ca-certificate"
            keyAlgorithm        "RSA"
            privateKeyPem       caPrivateKey.PrivateKeyPem
            isCaCertificate     true
            validityPeriodHours (1095 * 24)
            
            allowedUses [
                "cert_signing"
                "crl_signing"
            ]
            
            subjects [
                selfSignedCertSubject {
                    commonName "VPN CA"
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
            publicIPAllocationMethod IPAllocationMethod.Dynamic
            resourceGroup            rg.Name
            
            publicIPAddressSku {
                name "Basic"
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
                name VirtualNetworkGatewaySkuName.VpnGw1
                tier VirtualNetworkGatewaySkuTier.VpnGw1
            }
            
            gatewayType VirtualNetworkGatewayType.Vpn            
            vpnType     VpnType.RouteBased
            
            vpnClientConfiguration {
                vpnClientProtocols [ 
                    Union.FromT1 VpnClientProtocol.OpenVPN
                ]
                
                vpnClientRootCertificates [
                    vpnClientRootCertificate {
                        name           "P2SRootCert"
                        publicCertData (caCertificate.CertPem.Apply(X509.removeBeginEndCertificate))
                    }
                ]
                
                addressSpace {
                    addressPrefixes "172.16.201.0/24"
                }
            }
        }

    let clientPrivateKey =
        privateKey {
            name      "client-private-key"
            algorithm "RSA"
        }
    
    let clientCertificate =
        ClientCertificate.create caCertificate caPrivateKey clientPrivateKey "client"
    
    let additionalClientNames =
        Config().GetObject<string[]>("additionalClients")
        |> Option.ofObj
        |> Option.map List.ofArray
    
    let profileResult = VpnProfile.generate rg gateway
    
    let clientNameToOvpnFile =
           (fun (client : string)     -> client, ClientCertificate.create caCertificate caPrivateKey clientPrivateKey client)
        >> (fun (client, certificate) -> client, VpnProfile.getOpenVpnConfigurationFile certificate clientPrivateKey profileResult)
        >> (fun (client, ovpn)        -> $"OpenVpnFile-{client}", ovpn :> obj)
    
    let additionalOvpnFiles =
        additionalClientNames
        |> Option.map (List.map clientNameToOvpnFile)
        |> Option.defaultValue []
    
    ( "OpenVpnFile", VpnProfile.getOpenVpnConfigurationFile clientCertificate clientPrivateKey profileResult :> obj )
    :: additionalOvpnFiles
    |> dict
)