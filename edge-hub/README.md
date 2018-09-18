# Edge Hub
This project contains the Edge Hub.

## How to debug Edge Hub using Visual Studio
1. Set environment variable `EdgeModuleHubServerCertificateFile` to the path of a SSL Certificate file (e.g. C:\edgeDevice.pfx); for debug purpose, you can [create](https://docs.microsoft.com/en-us/azure/cloud-services/cloud-services-certs-create#create-a-new-self-signed-certificate) and use a self-signed certificate.  Remember to restart Visual Studio to take effect.
2. Update following values in appsettings_hub.json in Microsoft.Azure.Devices.Edge.Hub.Service project. 
  * `IotHubConnectionString` - Edge Hub module connection string; set it as edge device connection string appending with "ModuleId=$edgeHub".
  * `configSource` - Set it as `twin` to read confirm from twin, or `local` to read Edge Hub config from configuration file.
3. Set Microsoft.Azure.Devices.Edge.Hub.Service as startup project in Visual Studio.
4. Make sure to rebuild the solution.
5. You can start debugging Edge Hub by hit F5 in Visual Studio.