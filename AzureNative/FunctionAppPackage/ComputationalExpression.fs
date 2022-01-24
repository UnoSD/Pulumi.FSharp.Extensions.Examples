module Pulumi.FSharp.AzureNative.Components.FunctionAppPackage

open Pulumi.FSharp.AzureNative.Components.FunctionAppPackageInternals
open Pulumi.FSharp
open Pulumi

let private coalesceOptionsOrFail lOpt rOpt error =
    match lOpt, rOpt with
    | Some v, None
    | None  , Some v -> Some v
    | None  , None   -> None
    | Some _, Some _ -> failwith error

type FunctionAppPackageBuilderConfig =
    {
        WorkloadName: string option
        ResourceGroupName: Input<string> option
        ProjectPackagePublishPath: string option
    }

type FunctionAppPackageBuilder() =
    member _.Yield(_: unit) = {
        WorkloadName = None
        ResourceGroupName = None
        ProjectPackagePublishPath = None
    }
    
    member _.Run(args) =
        create args.WorkloadName.Value
               args.ResourceGroupName.Value
               args.ProjectPackagePublishPath.Value

    member this.Combine(lArgs, rArgs) = {
        WorkloadName              = coalesceOptionsOrFail lArgs.WorkloadName
                                                          rArgs.WorkloadName
                                                          "Duplicate workloadName in functionAppPackage CE"
        ResourceGroupName         = coalesceOptionsOrFail lArgs.ResourceGroupName
                                                          rArgs.ResourceGroupName
                                                          "Duplicate resourceGroupName in functionAppPackage CE"
        ProjectPackagePublishPath = coalesceOptionsOrFail lArgs.ProjectPackagePublishPath
                                                          rArgs.ProjectPackagePublishPath
                                                          "Duplicate projectPackagePublishPath in functionAppPackage CE"
    }

    [<CustomOperation("workloadName")>]
    member _.WorkloadName(args, name) = { args with WorkloadName = Some name }
    
    [<CustomOperation("resourceGroupName")>]
    member _.ResourceGroupName(args, name) = { args with ResourceGroupName = Some name }
    
    member _.ResourceGroupName(args, name) = { args with ResourceGroupName = Some <| input name }
    
    [<CustomOperation("projectPackagePublishPath")>]
    member _.ProjectPackagePublishPath(args, path) = { args with ProjectPackagePublishPath = Some path }
    
let functionAppPackage = FunctionAppPackageBuilder()