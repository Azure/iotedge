// Copyright (c) Microsoft. All rights reserved.
namespace ModuleRestarter
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("ModuleRestarter");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting module restarter with the following settings:\r\n{Settings.Current}");

            if (Settings.Current.DesiredModulesToRestart.Count == 0)
            {
                Logger.LogInformation("No modules names found in input. Stopping.");
                return 0;
            }

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await RestartModules(cts);

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("ModuleRestarter Main() finished.");
            return 0;
        }

        /// <summary>
        /// Restarts random modules periodically (with default restart occurrence once every 10 minutes)
        /// </summary>
        static async Task RestartModules(CancellationTokenSource cts)
        {
            try
            {
                ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);

                CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
                Random random = new Random();

                string payloadSchema = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
                List<string> moduleNames = Settings.Current.DesiredModulesToRestart;
                while (!cts.Token.IsCancellationRequested)
                {
                    string payload = string.Format(payloadSchema, moduleNames[random.Next(0, moduleNames.Count)]);
                    Logger.LogInformation("RestartModule Method Payload: {0}", payload);
                    c2dMethod.SetPayloadJson(payload);

                    try
                    {
                        CloudToDeviceMethodResult response = await iotHubServiceClient.InvokeDeviceMethodAsync(Settings.Current.DeviceId, "$edgeAgent", c2dMethod);
                        if (response.Status != (int)HttpStatusCode.OK)
                        {
                            Logger.LogError($"Calling Direct Method failed with status code {response.Status}.");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Exception caught for payload {payload}: {e}");
                    }

                    await Task.Delay(Settings.Current.RestartIntervalInMins * 60 * 1000, cts.Token);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception caught: {e}");
                throw;
            }

            Logger.LogInformation("RestartModules finished.");
        }
    }
}
