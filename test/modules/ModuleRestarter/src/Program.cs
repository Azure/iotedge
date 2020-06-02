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

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await RestartModules(cts);

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("ModuleRestarter Main() finished.");
            return 0;
        }

        /// <summary>
        /// Restarts random modules periodically (with default restart occurrence once every 10 minutes).
        /// </summary>
        static async Task RestartModules(CancellationTokenSource cts)
        {
            if (Settings.Current.DesiredModulesToRestart.Count == 0)
            {
                Logger.LogInformation("No modules names found in input. Stopping.");
                return;
            }

            try
            {
                ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);
                CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
                Random random = new Random();
                string payloadSchema = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
                List<string> moduleNames = Settings.Current.DesiredModulesToRestart;

                while (!cts.Token.IsCancellationRequested)
                {
                    Logger.LogInformation($"Started delay for {Settings.Current.RestartInterval} till {DateTime.UtcNow.Add(Settings.Current.RestartInterval)}.");
                    await Task.Delay(Settings.Current.RestartInterval, cts.Token);

                    if (!cts.IsCancellationRequested)
                    {
                        string moduleToBeRestarted = moduleNames[random.Next(0, moduleNames.Count)];
                        string payload = string.Format(payloadSchema, moduleToBeRestarted);
                        Logger.LogInformation("RestartModule Method Payload: {0}", payload);

                        try
                        {
                            c2dMethod.SetPayloadJson(payload);
                            CloudToDeviceMethodResult response = await iotHubServiceClient.InvokeDeviceMethodAsync(Settings.Current.DeviceId, "$edgeAgent", c2dMethod);
                            Logger.LogInformation($"Successfully invoke direct method to restart module {moduleToBeRestarted}.");

                            if (response.Status != (int)HttpStatusCode.OK)
                            {
                                Logger.LogError($"Calling Direct Method failed with status code {response.Status}.");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"Exception caught for payload {payload}: {e}");
                        }
                    }
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
