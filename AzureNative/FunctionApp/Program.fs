module Program

open Pulumi.FSharp.AzureNative.Components
open Pulumi.FSharp.Config
open Pulumi.FSharp

Deployment.run (fun () ->
    FunctionApp.create config.["workloadOrApplication"]
                       (input config.["resourceGroupName"])
                       config.["projectPublishPath"]
    
    dict []
)