module Infrastructure

open Pulumi.FSharp.Config

let infra () =
    ResourceGroup.create () |>
    KubernetesCluster.create config.["logAnalyticsWorkspaceId"]
                             config.["kubeletUserIdentityId"]
                             config.["kubeletUserIdentityClientId"]
                             config.["kubeletUserIdentityObjectId"]
                             config.["loadBalancerPublicIp"]
    
    // Set kubectl context
    
    let apiNamespace = Namespace.create "api-ns"
    let webNamespace = Namespace.create "web-ns"

    let userJavaPod = WebPod.createHighPerformance apiNamespace "userjava"    EnvironmentVariables.sql       
    let userProfile = WebPod.createLowPerformance  apiNamespace "userprofile" EnvironmentVariables.sql       
    let poi         = WebPod.createLowPerformance  apiNamespace "poi"         EnvironmentVariables.sql       
    let trips       = WebPod.createLowPerformance  apiNamespace "trips"       EnvironmentVariables.sql       
    let tripviewer  = WebPod.createLowPerformance  webNamespace "tripviewer"  EnvironmentVariables.tripViewer
    
    let userJavaService    = PodService.createClusterIp    userJavaPod
    let userProfileService = PodService.createClusterIp    userProfile
    let poiService         = PodService.createClusterIp    poi        
    let tripsService       = PodService.createClusterIp    trips      
    let tripviewerService  = PodService.createLoadBalancer tripviewer 

    NamespaceRole.create webNamespace "read-web-role" [ "get"; "list" ]
    NamespaceRole.create webNamespace "read-api-role" [ "get"; "list" ]
    NamespaceRole.create webNamespace "edit-web-role" [ "*" ]
    NamespaceRole.create webNamespace "edit-api-role" [ "*" ]

    Ingress.createWeb webNamespace tripviewerService

    Ingress.createApi apiNamespace userJavaService    "user-java"
    Ingress.createApi apiNamespace tripsService       "trips"
    Ingress.createApi apiNamespace poiService         "poi"
    Ingress.createApi apiNamespace userProfileService "user"

    dict []