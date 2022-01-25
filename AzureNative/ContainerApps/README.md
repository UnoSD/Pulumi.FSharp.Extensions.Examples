# ContainerApps

Creates an **Azure Container Apps** environment.

Deploys a sample container that reads from a queue and logs the message.

Currently **ACA** is in preview and the **REST API** schema is invalid, therefore the deployment of the resource will not work.

As a temporary workaround, it can be created with the **az cli** command in the comments and imported in the **Pulumi state**

```bash
pulumi config set azure-native:location "West Europe"
```