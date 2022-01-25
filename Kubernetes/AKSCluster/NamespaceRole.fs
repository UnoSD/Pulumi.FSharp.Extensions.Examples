module NamespaceRole

open Pulumi.FSharp.Kubernetes.Meta.V1.Inputs
open Pulumi.FSharp.Kubernetes.Rbac.V1.Inputs
open Pulumi.FSharp.Kubernetes.Rbac
open Pulumi

let create (roleNamespace : Output<string>) (roleName : string) (roleVerbs : string seq) =
    V1.role {
        name roleName

        objectMeta {
            ``namespace`` roleNamespace
        }

        rules [
            policyRule {
                resources "*"
                verbs     roleVerbs
                apiGroups ""
            }
        ]
    }