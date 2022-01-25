# AKSCluster

Creates a **Kubernetes cluster** in **Azure**.

It creates **namespaces**, **pods**, **services** and **ingress** to enable the deployment of the environment from the [Microsoft container OpenHack](https://github.com/Microsoft-OpenHack/containers_artifacts)

```bash
pulumi config set kubeletUserIdentityId: /subscriptions/YourSubscriptionId/resourceGroups/YourResourceGroup/providers/Microsoft.ManagedIdentity/userAssignedIdentities/YourUserMi
pulumi config set kubeletUserIdentityClientId: YourUserMiClientId
pulumi config set kubeletUserIdentityObjectId: YourUserMiObjectId
pulumi config set loadBalancerPublicIp: YourLbPipId
pulumi config set logAnalyticsWorkspaceId: /subscriptions/YourSubscriptionId/resourceGroups/YourResourceGroup/providers/Microsoft.OperationalInsights/workspaces/YourLaWorkspace
pulumi config set registryPath: yourAcr.azurecr.io/YourPrefix
pulumi config set --secret sqlPassword yourSqlPassword
pulumi config set sqlServer: youAzureSqlInstance.database.windows.net
pulumi config set sqlUser: yourSqlUser
pulumi config set azure-native:location "West Europe"
```