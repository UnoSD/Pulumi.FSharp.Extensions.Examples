module VPN.Region

open Pulumi

let short =
    match Config("azure-native").Require("location").ToLowerInvariant() with
    | "switzerland north" -> "swn"
    | "west europe"       -> "westeu"
    | "north europe"      -> "northeu"
    | region              -> region.Replace(" ", "")