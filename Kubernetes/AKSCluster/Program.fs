module Program

open Pulumi.FSharp

[<EntryPoint>]
let main _ = Deployment.run Infrastructure.infra
