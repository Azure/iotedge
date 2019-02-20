// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodCloudSender
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;
    using Message = Microsoft.Azure.Devices.Client.Message;
    using TransportType = Microsoft.Azure.Devices.Client.TransportType;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodCloudSender");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger.LogInformation("DirectMethodCloudSender Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string serviceClientConnectionString = Preconditions.CheckNonWhiteSpace(configuration.GetValue<string>("ServiceClientConnectionString"), "ServiceClientConnectionString");
            // Get device id of this device, exposed as a system variable by the iot edge runtime
            string targetDeviceId = configuration.GetValue<string>("IOTEDGE_DEVICEID");
            string targetModuleId = configuration.GetValue("TargetModuleId", "DirectMethodReceiver");
            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);
            TimeSpan dmDelay = configuration.GetValue("DirectMethodDelay", TimeSpan.FromSeconds(5));

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), null);

            await CallDirectMethodFromCloud(serviceClientConnectionString, targetDeviceId, targetModuleId, transportType, dmDelay, cts);

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("DirectMethodCloudSender Main() finished.");
            return 0;
        }

        static async Task CallDirectMethodFromCloud(
            string serviceClientConnectionString,
            string deviceId,
            string moduleId,
            TransportType transportType,
            TimeSpan delay,
            CancellationTokenSource cts)
        {
            Logger.LogInformation("CallDirectMethodFromCloud started.");
            ModuleClient moduleClient = null;
            ServiceClient serviceClient = null;

            try
            {
                Guid batchId = Guid.NewGuid();
                int count = 1;

                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(serviceClientConnectionString);
                Logger.LogInformation($"Prepare to call Direct Method from cloud ({iotHubConnectionStringBuilder.IotHubName}) on device [{deviceId}] module [{moduleId}]");

                serviceClient = ServiceClient.CreateFromConnectionString(serviceClientConnectionString, Microsoft.Azure.Devices.TransportType.Amqp);
                var cloudToDeviceMethod = new CloudToDeviceMethod("HelloWorldMethod").SetPayloadJson("{ \"Message\": \"Hello\" }");

                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    transportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                while (!cts.Token.IsCancellationRequested)
                {
                    Logger.LogInformation($"Calling Direct Method from cloud ({iotHubConnectionStringBuilder.IotHubName}) on device [{deviceId}] module [{moduleId}] of count {count}.");

                    try
                    {
                        CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(deviceId, moduleId, cloudToDeviceMethod, CancellationToken.None);

                        if (result.Status == (int)HttpStatusCode.OK)
                        {
                            var eventMessage = new Message(Encoding.UTF8.GetBytes($"Direct Method [{transportType}] Call succeeded."));
                            eventMessage.Properties.Add("sequenceNumber", count.ToString());
                            eventMessage.Properties.Add("batchId", batchId.ToString());
                            Logger.LogInformation($"Calling Direct Method from cloud with count {count} succeeded.");
                            await moduleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes("Direct Method Call succeeded.")));
                        }
                        else
                        {
                            Logger.LogError($"Calling Direct Method from cloud with count {count} failed with status code {result.Status}.");
                        }

                        count++;
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Exception caught with count {count}: {e}");
                    }

                    await Task.Delay(delay, cts.Token);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception caught: {e}");
                throw;
            }
            finally
            {
                Logger.LogInformation("Close connection for service client and module client");
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }

                if (moduleClient != null)
                {
                    await moduleClient.CloseAsync();
                }
            }

            Logger.LogInformation("CallDirectMethodFromCloud finished.");
        }
    }
}
