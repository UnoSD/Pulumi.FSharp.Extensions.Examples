module Program

open Pulumi.FSharp.AzureNative.Components.FunctionAppPackage
open Pulumi.FSharp.Config
open Pulumi.FSharp
open Pulumi

Deployment.run (fun () ->
    let functionAppInfrastructure =
        functionAppPackage {
            workloadName              config.["workloadOrApplication"]
            resourceGroupName         config.["resourceGroupName"]
            projectPackagePublishPath config.["projectPublishPath"]
        }
    
    dict [ "Test URL", Output.Format($"https://{functionAppInfrastructure.App.DefaultHostName}/api/Echo?test") :> obj ]
)