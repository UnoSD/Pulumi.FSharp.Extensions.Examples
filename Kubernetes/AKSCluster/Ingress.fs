module Ingress

open Pulumi.FSharp.Kubernetes.Networking.V1Beta1.Inputs
open Pulumi.FSharp.Kubernetes.Meta.V1.Inputs
open Pulumi.FSharp.Kubernetes.Networking
open Pulumi

let private pathToBackend (urlPath : string) (backEndServiceName : Output<string>) =
    ingressSpec {
        rules [
            ingressRule {
                hTTPIngressRuleValue {
                    paths [
                        hTTPIngressPath {
                            path     urlPath
                            ingressBackend {
                                serviceName backEndServiceName
                                servicePort 80
                            }
                        }
                    ]
                }
            }
        ]
    }

let createWeb (webNamespace : Output<string>) serviceName =
    V1Beta1.ingress {
        name "public-igr"
    
        objectMeta {
            ``namespace`` webNamespace
    
            annotations [
                "kubernetes.io/ingress.class"               , "nginx"
                "nginx.ingress.kubernetes.io/ssl-redirect"  , "false"
                "nginx.ingress.kubernetes.io/use-regex"     , "true"
                "nginx.ingress.kubernetes.io/rewrite-target", "/$1"
            ]
        }
    
        pathToBackend "/" serviceName
    }

let createApi (apiNamespace : Output<string>) serviceName segment =
    V1Beta1.ingress {
        name $"{segment}api-igr"

        objectMeta {
            ``namespace`` apiNamespace
            annotations   [ "kubernetes.io/ingress.class", "nginx" ]
        }

        pathToBackend $"/api/{segment}" serviceName
    }