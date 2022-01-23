# DevelopmentVM

Creates a Windows **Virtual Machine** in **Azure**.

It installs also **Chocolatey**, **Visual Studio** and **ReSharper**.

The script also mounts an **Azure Files** instance an maps it as the network drive **Z:** for persistent storage.

Replace the default configuration with the preferred as needed (for security reason is **highly recommended** to replace the **VM** password)

```bash
pulumi config set azure-native:location "West Europe"
pulumi config set shareName share
pulumi config set storageAccount yourStorageAccount
pulumi config set storageSubscription yourStorageSubscription
pulumi config set --secret storageKey yourStorageKey
pulumi config set vmDnsLabel yourDnsLabel
pulumi config set --secret vmPass P@ssw0rd1_
pulumi config set vmShutdownNotifyEmail your@email.com
pulumi config set vmUser yourVmUser
```