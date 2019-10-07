// Copyright (c) Microsoft. All rights reserved.
namespace ModuleRestarter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<Program>();
        static readonly string IoTHubConnectionString = Settings.Current.IoTHubConnectionString;
        static readonly string DeviceId = Settings.Current.DeviceId;
        static readonly string DesiredModulesToRestartCSV = Settings.Current.DesiredModulesToRestartCSV;
        static readonly int RandomRestartIntervalInMins = Settings.Current.RandomRestartIntervalInMins;

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            if (string.IsNullOrEmpty(DesiredModulesToRestartCSV))
            {
                Log.LogInformation("No modules specified to restart. Stopping.");
                return 0;
            }

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromMinutes(2 * RandomRestartIntervalInMins), null);

            await RestartModules(cts, RandomRestartIntervalInMins);
            completed.Set();

            return 0;
        }

        /// <summary>
        /// Restarts random modules periodically (with default restart occurrence once every 5 minutes)
        /// </summary>
        static async Task RestartModules(CancellationTokenSource cts, int randomRestartIntervalInMins)
        {
            Log.LogInformation("Device ID: {0}", DeviceId);
            Log.LogInformation("Module CSV received: {0}", DesiredModulesToRestartCSV);
            Log.LogInformation("Random restart interval: {0}", RandomRestartIntervalInMins);

            string[] moduleNames = DesiredModulesToRestartCSV.Split(",");
            ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(IoTHubConnectionString);
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            Random random = new Random();

            while (!cts.Token.IsCancellationRequested)
            {
                string payload = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
                payload = string.Format(payload, moduleNames[random.Next(0, moduleNames.Length)]);
                Log.LogInformation("RestartModule Method Payload: {0}", payload);
                c2dMethod.SetPayloadJson(payload);

                await iotHubServiceClient.InvokeDeviceMethodAsync(DeviceId, "$edgeAgent", c2dMethod);

                Thread.Sleep(RandomRestartIntervalInMins * 60 * 1000);
            }
        }
    }
}
