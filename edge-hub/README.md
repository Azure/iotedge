# Edge Hub
This project contains the Edge Hub.

## How to debug Edge Hub using Visual Studio
1. Update following values in appsettings_agent.json in Microsoft.Azure.Devices.Edge.Hub.Service project. 
  * `IotHubConnectionString` - Edge Hub module connection string; set it as edge device connection string appending with "ModuleId=$edgeHub".
  * `configSource` - Set it as `twin` to read confirm from twin, or `local` to read Edge Hub config from configuration file.
2. Set following environment variables to point to the path of a SSL Certificate; for debug purpose, you can [create](https://docs.microsoft.com/en-us/azure/cloud-services/cloud-services-certs-create#create-a-new-self-signed-certificate) and use a self-signed certificate.
  * `SSL_CERTIFICATE_NAME` - certificate file name.
  * `SSL_CERTIFICATE_PATH` - path of folder containing the certificate.
3. Set Microsoft.Azure.Devices.Edge.Hub.Service as startup project in Visual Studio.
4. Make sure to rebuild the solution.
5. You can start debugging Edge Hub by hit F5 in Visual Studio.