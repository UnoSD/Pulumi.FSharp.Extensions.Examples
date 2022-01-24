module Pulumi.FSharp.AzureNative.Components.FunctionAppPackage

open Pulumi.FSharp.AzureNative.Components.FunctionAppPackageInternals

type FunctionAppPackageBuilderConfig =
    {
        WorkloadName: string option
        ResourceGroupName: string option
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
        WorkloadName              = match lArgs.WorkloadName, rArgs.WorkloadName with
                                    | Some wn, None
                                    | None   , Some wn -> Some wn
                                    | None   , None    -> None
                                    | Some _ , Some _  -> failwith "Duplicate workloadName in functionAppPackage CE"
        ResourceGroupName         = match lArgs.ResourceGroupName, rArgs.ResourceGroupName with
                                    | Some wn, None
                                    | None   , Some wn -> Some wn
                                    | None   , None    -> None
                                    | Some _ , Some _  -> failwith "Duplicate resourceGroupName in functionAppPackage CE"
        ProjectPackagePublishPath = match lArgs.ProjectPackagePublishPath, rArgs.ProjectPackagePublishPath with
                                    | Some wn, None
                                    | None   , Some wn -> Some wn
                                    | None   , None    -> None
                                    | Some _ , Some _  -> failwith "Duplicate projectPackagePublishPath in functionAppPackage CE"
    }

    [<CustomOperation("workloadName")>]
    member _.WorkloadName(args, name) = { args with WorkloadName = Some name }
    
    [<CustomOperation("resourceGroupName")>]
    member _.ResourceGroupName(args, name) = { args with ResourceGroupName = Some name }
    
    [<CustomOperation("projectPackagePublishPath")>]
    member _.ProjectPackagePublishPath(args, path) = { args with ProjectPackagePublishPath = Some path }
    
let functionAppPackage = FunctionAppPackageBuilder()