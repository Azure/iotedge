# Edge Hub
This project contains the Edge Hub.

## How to debug Edge hub using Visual Studio
1. Set environment variable `EdgeModuleHubServerCertificateFile` to the path of a SSL Certificate file (e.g. C:\edgeDevice.pfx); for debug purpose, you can [create](https://docs.microsoft.com/en-us/azure/cloud-services/cloud-services-certs-create#create-a-new-self-signed-certificate) and use a self-signed certificate.  Remember to restart Visual Studio to take effect.
2. Update following values in appsettings_hub.json in Microsoft.Azure.Devices.Edge.Hub.Service project. 
  * `IotHubConnectionString` - Edge Hub module connection string; set it as edge device connection string appending with "ModuleId=$edgeHub".
  * `configSource` - Set it as `twin` to read confirm from twin, or `local` to read Edge Hub config from configuration file.
3. Set Microsoft.Azure.Devices.Edge.Hub.Service as startup project in Visual Studio.
4. Make sure to rebuild the solution.
5. You can start debugging Edge Hub by hit F5 in Visual Studio.

##### If you want to run a leaf device connecting to Edge hub using Visual Studio, then continue to read below:
1. You can either use samples in [Azure IoT C# SDK](https://github.com/azure/azure-iot-sdk-csharp) or write your write your own leaf device application using DeviceClient in Azure IoT C# SDK.
2. I will use Direct method sample in Azure IoT C# SDK, you can find it under iothub\device\samples\DeviceClientMethodSample.
3. Open that sample application in Visual Studio, update connection string and add `GatewayHostName=127.0.0.1` to it in order to connect to Edge hub running locally in debugging mode in Visual Studio.
4. Make sure Edge hub is running first, then you can start sample application by hit F5 in Visual Studio.
5. Look at Edge hub console window, you will see logs about connecting to a new device.
6. You can invoke direct method call from Azure IoT Portal or from Device Explorer.
  * `Method name`: WriteToConsole
  * `Method payload`: Any valid json
7. You should see your method payload printed out in leaf device console window.
