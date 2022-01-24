# DevelopmentVM

Creates a **Function App** in **Azure** from a package published locally.

Publish your package with `dotnet publish` and zip the content.

It creates:

* App Service Plan
* Storage Account to push the package
* Application Insight
* Key Vault with application secrets
* Function App

```bash
pulumi config set azure-native:location "West Europe"
pulumi config set resourceGroupName resourceGroupToDeployFunction
pulumi config set workloadOrApplication nameForResources # rg-NAME-STACK-REGION-00X
pulumi config set projectPublishPath locationOfZipFilePublishedFunctionApp
```