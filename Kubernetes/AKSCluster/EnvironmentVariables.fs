module EnvironmentVariables

open Pulumi.FSharp.Kubernetes.Core.V1.Inputs
open Pulumi.FSharp.Config

let sql =
    [
        envVar {
            name  "SQL_USER"
            value config.["sqlUser"]
        }
        envVar {
            name  "SQL_PASSWORD"
            value config.["sqlPassword"]
        }
        envVar {
            name  "SQL_SERVER"
            value config.["sqlServer"]
        }
    ]

let tripViewer =
    [
        envVar {
            name  "TRIPS_API_ENDPOINT"
            value "http://trips-service.default.svc.cluster.local"
        }
        envVar {
            name  "USERPROFILE_API_ENDPOINT"
            value "http://userprofile-service.default.svc.cluster.local"
        }
    ]