module VPN.X509

open System.Security.Cryptography.X509Certificates
open System

let removeBeginEndCertificate (pemCert : string) = 
    X509Certificate2.CreateFromPem(pemCert.AsSpan())
                    .Export(X509ContentType.Cert) |>
    Convert.ToBase64String