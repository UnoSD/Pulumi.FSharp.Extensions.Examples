# LinuxP2SOpenVPN

Creates a **Public IP**, a **Virtual Network** and a **VPN Gatway** gateway in **Azure**;

It creates then a **P2S** configuration with **OpenVPN** generating all the certificates and keys within **Pulumi**.

Once the deployment is completed, it will output a valid **OpenVPN** configuration file, use:

`pulumi stack output OpenVpnFile --show-secrets > AzureVPN.ovpn`

And you can then import the file in a VPN client and connect straightway.

Works also on **GNU/Linux**; tested with the **NetworkManager** UI, importing the output file.