module VPN.ClientCertificate

open Pulumi.FSharp.Tls.Inputs
open Pulumi.FSharp.Tls
open Pulumi.Tls

let create (caCertificate : SelfSignedCert)
           (caPrivateKey : PrivateKey)
           (clientPrivateKey : PrivateKey)
           (clientName : string) =    
    let clientCertificateRequest =
        certRequest {
            name          $"individual-certificate-request-{clientName}"
            keyAlgorithm  "RSA"
            privateKeyPem clientPrivateKey.PrivateKeyPem
            dnsNames      clientName
            
            subjects      [
                certRequestSubject {
                    commonName clientName
                }
            ]
        }

    locallySignedCert {
        name                $"individual-certificate-{clientName}"
        caCertPem           caCertificate.CertPem
        caKeyAlgorithm      "RSA"
        caPrivateKeyPem     caPrivateKey.PrivateKeyPem
        certRequestPem      clientCertificateRequest.CertRequestPem
        validityPeriodHours (1095 * 24)
        allowedUses         "client_auth"
    }