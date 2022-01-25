module ResourceGroup

open Pulumi.FSharp.AzureNative.Resources
open Pulumi

let create () =
    resourceGroup {
        name          $"rg-ohc-{Deployment.Instance.StackName}-weu-001"
    }