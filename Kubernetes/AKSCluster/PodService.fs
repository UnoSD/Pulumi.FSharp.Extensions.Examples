module PodService

open Pulumi.FSharp.Kubernetes.Core.V1.Inputs
open Pulumi.FSharp.Kubernetes.Meta.V1.Inputs
open Pulumi.FSharp.Kubernetes.Core
open Pulumi.Kubernetes.Core.V1
open Pulumi.FSharp.Outputs

let private create (pod : Pod) (serviceType : ServiceSpecType) =
    output {
        let! meta = pod.Metadata

        let service =
            V1.service {
                name meta.Name

                objectMeta {
                    name          $"{meta.Name}-service"
                    ``namespace`` meta.Namespace
                }

                serviceSpec {
                    resourceType serviceType
                    ports        [ servicePort { port 80 } ]
                    selector     [ "app", meta.Name ]
                }
            }

        let! svcMeta = service.Metadata

        return svcMeta.Name
    }

let createClusterIp pod =
    create pod ServiceSpecType.ClusterIP

let createLoadBalancer pod =
    create pod ServiceSpecType.LoadBalancer