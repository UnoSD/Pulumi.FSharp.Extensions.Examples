module Program

open Pulumi.FSharp.AzureNative.Components.FunctionAppPackage
open Pulumi.FSharp.Config
open Pulumi.FSharp

Deployment.run (fun () ->
    let functionAppInfrastructure =
        functionAppPackage {
            workloadName              config.["workloadOrApplication"]
            resourceGroupName         config.["resourceGroupName"]
            projectPackagePublishPath config.["projectPublishPath"]
        }
    
    dict [ "Function ID", functionAppInfrastructure.App.Id :> obj ]
)