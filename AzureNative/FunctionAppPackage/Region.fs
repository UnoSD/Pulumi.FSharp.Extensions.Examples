module Pulumi.FSharp.AzureNative.Components.FunctionAppPackageInternalsRegion

open Pulumi

let short =
    match Config("azure-native").Require("location").ToLowerInvariant() with
    | "switzerland north" -> "swn"
    | "west europe"       -> "weu"
    | "north europe"      -> "neu"
    | region              -> region.Replace(" ", "")