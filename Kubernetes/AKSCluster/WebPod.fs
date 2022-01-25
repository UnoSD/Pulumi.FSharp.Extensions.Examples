module WebPod

open Pulumi.FSharp.Kubernetes.Core.V1.Inputs
open Pulumi.FSharp.Kubernetes.Meta.V1.Inputs
open Pulumi.Kubernetes.Types.Inputs.Core.V1
open Pulumi.FSharp.Kubernetes.Core
open Pulumi.FSharp.Outputs
open Pulumi.FSharp.Config
open Pulumi

let private registryPath =
    config.["registryPath"]

let private create (podNamespace : Output<string>)
                   podName
                   (requestMemory : string)
                   requestCpu
                   (limitMemory : string)
                   limitCpu
                   (environmentVariables : EnvVarArgs list) =
    V1.pod {
        name podName

        objectMeta {
            name          podName
            ``namespace`` podNamespace
            labels        [ "app", podName ]
        }

        podSpec {
            containers [
                container {
                    name  "web"
                    image $"{registryPath}/{podName}-image:version1"
                    ports [ 
                        containerPort {
                            name               "web"
                            protocol           "TCP"
                            containerPortValue 80
                        }
                    ]

                    resourceRequirements {
                        requests [
                            "memory", requestMemory
                            "cpu"   , requestCpu
                        ]
                        limits [
                            "memory", limitMemory
                            "cpu"   , limitCpu
                        ]
                    }

                    env environmentVariables
                }
            ]
        }
    }

let private createGetName podNamespace
                          podName
                          requestMemory
                          requestCpu
                          limitMemory
                          limitCpu
                          environmentVariables =
    output {
        let pod = create podNamespace
                         podName
                         requestMemory
                         requestCpu
                         limitMemory
                         limitCpu
                         environmentVariables

        let! meta = pod.Metadata

        return meta.Name
    }

let createLowPerformance podNamespace podName =
    create podNamespace podName "64Mi" "250m" "128Mi" "500m"

let createHighPerformance podNamespace podName =
    create podNamespace podName "256Mi" "500m" "512Mi" "750m"