module VPN.VpnProfile

open Pulumi.AzureNative.Authorization
open FSharp.Data.HttpRequestHeaders
open FSharp.Data.HttpContentTypes
open Pulumi.AzureNative.Resources
open Pulumi.AzureNative.Network
open System.IO.Compression
open Pulumi.FSharp.Outputs
open System.Threading
open Pulumi.FSharp
open FSharp.Data
open Pulumi.Tls
open System.IO
open Pulumi

let generate (rg : ResourceGroup) (gateway : VirtualNetworkGateway) =
    let headers =
        output {
            let! gctResult =
                GetClientToken.InvokeAsync()

            return [
                Authorization $"Bearer {gctResult.Token}"
                ContentType   Json
            ]
        }
            
    output {
        let! gccResult =
            GetClientConfig.InvokeAsync()
      
        let! url =
            Output.Format($"https://management.azure.com/subscriptions/{gccResult.SubscriptionId}/resourceGroups/{rg.Name}/providers/Microsoft.Network/virtualNetworkGateways/{gateway.Name}/generatevpnprofile")
        
        let! headers =
            headers
        
        let rec getUrl (response : HttpResponse) =
            match response.StatusCode, response.Body with
            | 200 , _        -> response.Headers
                                |> Map.tryFind "Location"
                                |> function
                                   | Some url -> Ok url
                                   | None     -> Error "No location"
            | 202 , _        -> Thread.Sleep(500)
                                response.Headers
                                |> Map.tryFind "Location"
                                |> function
                                   | Some url -> Http.Request(url, headers = headers) |> getUrl
                                   | None     -> Error "No location"
            | code, Text err -> Error $"{code}: {err}"
            | code, _        -> Error $"{code}"
        
        return
            Http.Request(url        = url,
                         query      = [ "api-version", "2021-03-01" ],
                         headers    = headers,
                         httpMethod = HttpMethod.Post,
                         body       = TextRequest """{ "authenticationMethod": "EAPTLS" }""")
            |> getUrl
            |> Result.map (fun url -> Http.RequestString(url, headers = headers).Trim('"'))
            |> Result.map (fun url -> Http.RequestStream(url).ResponseStream)
            |> Result.map (fun str -> new ZipArchive(str))
            |> Result.map (fun arc -> arc.GetEntry("OpenVPN\\vpnconfig.ovpn").Open())
            |> Result.map (fun str -> new StreamReader(str))
            |> Result.map (fun sre -> sre.ReadToEnd())
    }

let getOpenVpnConfigurationFile (clientCertificate : LocallySignedCert)
                                (clientPrivateKey : PrivateKey)
                                (profile : Output<Result<string,string>>) =
    let getConfig () =
        output {
            let! profile =
                profile
            
            let! clientCertificate =
                clientCertificate.CertPem
            
            let! clientPrivateKey =
                clientPrivateKey.PrivateKeyPem
            
            let stream =
                profile |>
                Result.map (fun ovp -> ovp.Replace("$CLIENTCERTIFICATE", clientCertificate.TrimEnd('\n'))
                                          .Replace("$PRIVATEKEY", clientPrivateKey.TrimEnd('\n')))
            
            return match stream with | Ok ovpnfile -> ovpnfile | Error err -> err
        }

    match Deployment.Instance.IsDryRun with
    | true  -> Output.Create("Run pulumi up to get output")
    | false -> getConfig ()