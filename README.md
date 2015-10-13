# CloudFlareDDNS
Use CloudFlare as DDNS


## Building

Before you build a project, you have to modify some settings.

1. Open Properties/Settings.settings in Solution Explorer.
2. In user scope, add credentials to access your CloudFlare account.
3. In application scope, modify environmental values.


## Using

To run this program, you must install it as service.
Run following command in cmd.

```
sc create [SERVICE_NAME] binPath= [BIN_PATH] start= auto
net start [SERVICE_NAME]
```
