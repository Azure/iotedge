// Copyright (c) Microsoft. All rights reserved.
namespace ModuleRestarter
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("ModuleRestarter");
        static readonly string ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(Settings.Current.ServiceClientConnectionString, "ServiceClientConnectionString");
        static readonly string DeviceId = Preconditions.CheckNonWhiteSpace(Settings.Current.DeviceId, "DeviceId");
        static readonly string DesiredModulesToRestartCSV = Settings.Current.DesiredModulesToRestartCSV;
        static readonly int RestartIntervalInMins = Settings.Current.RandomRestartIntervalInMins;

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
        /// Restarts random modules periodically (with default restart occurrence once every 10 minutes)
        /// </summary>
        static async Task RestartModules(CancellationTokenSource cts)
        {
            List<string> moduleNames = new List<string>();
            foreach (string name in DesiredModulesToRestartCSV.Split(","))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    moduleNames.Add(name);
                }
            }

            ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(ServiceClientConnectionString);
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            Random random = new Random();

            while (!cts.Token.IsCancellationRequested)
            {
                string payload = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
                payload = string.Format(payload, moduleNames[random.Next(0, moduleNames.Count)]);
                Logger.LogInformation("RestartModule Method Payload: {0}", payload);
                c2dMethod.SetPayloadJson(payload);

                await iotHubServiceClient.InvokeDeviceMethodAsync(DeviceId, "$edgeAgent", c2dMethod);

                await Task.Delay(RestartIntervalInMins * 60 * 1000, cts.Token);
            }
        }
    }
}
