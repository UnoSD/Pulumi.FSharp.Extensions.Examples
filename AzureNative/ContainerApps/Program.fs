module Program

open Pulumi.FSharp.AzureNative.OperationalInsights
open Pulumi.FSharp.AzureNative.Storage.Inputs
open Pulumi.AzureNative.OperationalInsights
open Pulumi.FSharp.AzureNative.Web.Inputs
open Pulumi.FSharp.AzureNative.Resources
open Pulumi.FSharp.AzureNative.Storage
open Pulumi.FSharp.AzureNative.Web
open Pulumi.AzureNative.Storage
open Pulumi.AzureNative.Web
open Pulumi.FSharp
open ContainerApps
open Pulumi

Deployment.run (fun () ->
    let rg =
        resourceGroup {
            name $"rg-capp-{Deployment.Instance.StackName}-{Region.short}-001"
        }

    let laWorkspace =
        workspace {
            name          $"la-capp-{Deployment.Instance.StackName}-{Region.short}-001"
            resourceGroup rg.Name
        }

    let laWorkspaceSharedKey =
        GetSharedKeys.Invoke(GetSharedKeysInvokeArgs(ResourceGroupName = rg.Name, WorkspaceName = laWorkspace.Name))
                     .Apply(fun r -> r.PrimarySharedKey)

    (*
    az containerapp env create \
      --name ke-capp-dev-weu-001<random> \
      --resource-group <rg.Name> \
      --logs-workspace-id <laWorkspace.CustomerId> \
      --logs-workspace-key <laWorkspaceSharedKey> \
      --location "West Europe"
    *)
    let kubeEnv =
        kubeEnvironment {
            name          $"ke-capp-{Deployment.Instance.StackName}-{Region.short}-001"
            resourceGroup rg.Name
            //``type``      "Managed"
            //environmentType "Managed"
            
            // From import
            kind     "containerenvironment"
            staticIp "20.86.121.168"
            // End from import
            
            appLogsConfiguration {
                // From import
                destination "log-analytics"
                // End from import
                
                logAnalyticsConfiguration {
                    customerId laWorkspace.CustomerId
                    // From import
                    //sharedKey  laWorkspaceSharedKey
                    // End from import
                }
            }
        }
    
    let storage =
        storageAccount {
            name          $"sacapp{Deployment.Instance.StackName}{Region.short}001"
            resourceGroup rg.Name
            kind          Kind.StorageV2
            sku           { name SkuName.Standard_LRS }
        }
    
    let storageQueue =
        queue {
            name          "caqueue"
            accountName   storage.Name
            resourceGroup rg.Name
        }
    
    let connectionString =
        ListStorageAccountKeys.Invoke(ListStorageAccountKeysInvokeArgs(AccountName = storage.Name,
                                                                       ResourceGroupName = rg.Name))
                              .Apply(fun r -> r.Keys |> Seq.map (fun k -> k.Value) |> Seq.head)
                              .Apply<string>(fun k -> Output.Format($"DefaultEndpointsProtocol=https;AccountName={storage.Name};AccountKey={k};EndpointSuffix=core.windows.net"))
    
    let secretName =
        "queueconnection"
    
    containerApp {
        name              $"ca-capp-{Deployment.Instance.StackName}-{Region.short}-001"
        resourceGroup     rg.Name
        kind              "containerApp"
        kubeEnvironmentId kubeEnv.Id
        
        configuration {
            activeRevisionsMode ActiveRevisionsMode.Single
            
            secrets [
                secret {
                    name  secretName
                    value connectionString
                }
            ]
        }
        
        template {
            scale {
                maxReplicas 10
                minReplicas 0
                
                rules [
                    scaleRule {
                        name "queuerule"
                        
                        queueScaleRule {
                            queueLength 100
                            queueName   storageQueue.Name
                            
                            auth        [
                                scaleRuleAuth {
                                    secretRef        secretName
                                    triggerParameter "connection"
                                }
                            ]
                        }
                    }
                ]
            }
        
            containers [
                container {
                    image "mcr.microsoft.com/azuredocs/containerapps-queuereader"
                    name  "queuereader"
                    
                    env [
                        environmentVar {
                            name  "QueueName"
                            value storageQueue.Name
                        }
                        environmentVar {
                            name      "QueueConnectionString"
                            secretRef secretName
                        }
                    ]
                }
            ]
        }
    }

    let checkLogs =
        Output.Format($"""az monitor log-analytics query --workspace {laWorkspace.CustomerId} --analytics-query "ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'queuereader' and Log_s contains 'Message ID'" --out table""")

    let addQueueMessages =
        Output.Format($"""az storage message put --content "Hello Queue Reader App" --queue-name "{storageQueue.Name}" --connection-string '{connectionString}'""")
    
    dict [ "Check logs"      , checkLogs        :> obj
           "Enqueue messages", addQueueMessages :> obj ]
)