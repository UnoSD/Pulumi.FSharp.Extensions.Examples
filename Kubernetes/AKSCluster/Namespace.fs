module Namespace

open Pulumi.FSharp.Outputs

let ``namespace`` = Pulumi.FSharp.Kubernetes.Core.V1.``namespace``

let create namespaceName =
    output {
        let ns =
            ``namespace`` {
                name namespaceName
            }
        
        let! nsMeta = ns.Metadata

        return nsMeta.Name
    }