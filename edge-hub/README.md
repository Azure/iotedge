# Edge Hub
This project contains the Edge Hub.

## Startup execution flow
| Sequence | Description | Code reference |
|----------|-------------|----------------|
|1 | Edge hub service starts. | `Program.Main` |
|2 | Load configuration from application json file (appsettings_hub.json) and environment variables. | `Program.Main` |
|3 | Load certificates for Edge hub and install certificate chain if available. |  |
|4 | Initialize web hosting (ASP.Net Core) and build dependency injection container. | `Hosting`, `IModule.Load` |
|5 | Instantiate each protocol including amqp, mqtt and http if it is enabled. | `IProtocolHead`
|6 | Start enabled protocol(s) asynchronously.  Each protocol will start a listener for incoming requests. | `IProtocolHead.StartAsync` |


## How to debug Edge hub using Visual Studio
1. Create an IOT edgeHub module connection string.
Create a Visual studio project following process here: https://docs.microsoft.com/azure/iot-hub/iot-hub-portal-csharp-module-twin-getstarted Section Update the module twin using .NET device SDK
but replace the program by the snippet below:
```C#
using System;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        const string connectionString = "Put iothubowner connection string here";
        const string deviceID = "Put IOT edge device ID here";

        static void Main(string[] args)
        {
            AddModuleAsync().Wait();
        }

        private static async Task AddModuleAsync()
        {
            RegistryManager registryManager =
             RegistryManager.CreateFromConnectionString(connectionString);
            Module parentEdgeHub;

            try
            {
                parentEdgeHub = await registryManager.GetModuleAsync(deviceID, "$edgeHub");
                parentEdgeHub.Authentication.Type = AuthenticationType.Sas;
                parentEdgeHub = await registryManager.UpdateModuleAsync(parentEdgeHub);
            }
            catch (ModuleAlreadyExistsException)
            {
                parentEdgeHub = await registryManager.GetModuleAsync(deviceID, "$edgeHub");
            }

            Console.WriteLine("Generated module key: {0}", parentEdgeHub.Authentication.SymmetricKey.PrimaryKey);
        }
    }
}
```
That snippet should create connection string for the module $edgeAgent. 

2. Set environment variable
  * `EdgeModuleHubServerCertificateFile` to the path of a SSL Certificate file (e.g. C:\edgeDevice.pfx); for debug purpose, you can [create](https://docs.microsoft.com/azure/cloud-services/cloud-services-certs-create#create-a-new-self-signed-certificate) and use a self-signed certificate.  Remember to restart Visual Studio to take effect.
  * `EdgeModuleHubServerCAChainCertificateFile` to the path of the CA. If certificate is self signed then is it the same as above;
Or
  * `EdgeHubDevServerCertificateFile` to the path of a SSL Certificate file (e.g. C:\edgeDevice.pfx); for debug purpose, you can [create](https://docs.microsoft.com/azure/cloud-services/cloud-services-certs-create#create-a-new-self-signed-certificate) and use a self-signed certificate.  Remember to restart Visual Studio to take effect.
  * `EdgeHubDevTrustBundleFile` to the path of the CA. If certificate is self signed then is it the same as above;
  * `EdgeHubDevServerPrivateKeyFile` to the path of the private key (*.key file)
3. Update following values in appsettings_hub.json in Microsoft.Azure.Devices.Edge.Hub.Service project. 
  * `IotHubConnectionString` - Copy the $edgeHub module connection string. It should have been created in step 1.
  * `configSource` - Set it as `twin` to read confirm from twin, or `local` to read Edge Hub config from configuration file.
3. Set Microsoft.Azure.Devices.Edge.Hub.Service as startup project in Visual Studio.
4. Make sure to rebuild the solution.
5. You can start debugging Edge Hub by hitting F5 in Visual Studio.

#### If you want to run a leaf device connecting to Edge hub using Visual Studio, then continue to read below:
1. You can either use samples in [Azure IoT C# SDK](https://github.com/azure/azure-iot-sdk-csharp) or write your own leaf device application using DeviceClient in Azure IoT C# SDK.
Make sure to note the hostname you write down in the certificate. If that hostname is not an IP address please follow step 3b too.
2. This example uses the direct method sample in Azure IoT C# SDK, you can find it under iothub\device\samples\DeviceClientMethodSample.
3. Open that sample application in a new instance of Visual Studio, update connection string and add `GatewayHostName="Host name you have put in the certificate` to it in order to connect to Edge hub running locally using Visual Studio.
3b. If the host name is not IP address you will have to alias the host name to the local ip address: 127.0.0.1;
In Windows edit with administrator rights c:\windows\system32\drivers\etc\hosts and add the line: "127.0.0.1      your hostname"
4. Make sure Edge hub is running first, then you can start sample application by hit F5 in Visual Studio.
5. Look at Edge hub console window, you will see logs about connecting to a new device.
6. You can invoke direct method call from Azure IoT Portal or from Device Explorer.
  * `Method name`: WriteToConsole
  * `Method payload`: Any valid json
7. You should see your method payload printed out in sample application console window.
