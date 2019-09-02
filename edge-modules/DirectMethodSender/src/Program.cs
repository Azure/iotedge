// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodSender");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger.LogInformation("DirectMethodSender Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            TimeSpan dmDelay = configuration.GetValue("DirectMethodDelay", TimeSpan.FromSeconds(5));
            // Get device id of this device, exposed as a system variable by the iot edge runtime
            string targetDeviceId = configuration.GetValue<string>("IOTEDGE_DEVICEID");
            string targetModuleId = configuration.GetValue("TargetModuleId", "DirectMethodReceiver");
            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);

            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                transportType,
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                Logger);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await CallDirectMethod(moduleClient, dmDelay, targetDeviceId, targetModuleId, cts);
            await moduleClient.CloseAsync();
            await cts.Token.WhenCanceled();

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("DirectMethodSender Main() finished.");
            return 0;
        }

        static async Task CallDirectMethod(
            ModuleClient moduleClient,
            TimeSpan delay,
            string deviceId,
            string moduleId,
            CancellationTokenSource cts)
        {
            var request = new MethodRequest("HelloWorldMethod", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));

            while (!cts.Token.IsCancellationRequested)
            {
                Logger.LogInformation($"Calling Direct Method on device [{deviceId}] module [{moduleId}].");

                try
                {
                    MethodResponse response = await moduleClient.InvokeMethodAsync(deviceId, moduleId, request);

                    if (response.Status == (int)HttpStatusCode.OK)
                    {
                        await moduleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes("Direct Method Call succeeded.")));
                        break;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception caught: {e}");
                }

                await Task.Delay(delay, cts.Token);
            }
        }
    }
}
