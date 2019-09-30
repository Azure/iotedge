// Copyright (c) Microsoft. All rights reserved.
namespace ModuleRestarter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;

    class Program
    {
        static readonly string connectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString");
        static readonly string deviceId = Environment.GetEnvironmentVariable("DeviceId");
        static readonly string modulesCSV = Environment.GetEnvironmentVariable("DesiredModulesToRestartJsonArray");
        static readonly string[] moduleNames = modulesCSV.Split(",");
        static readonly string randomRestartIntervalInMinsString = Environment.GetEnvironmentVariable("RandomRestartIntervalInMins");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            int randomRestartIntervalInMins = 5;
            if (!string.IsNullOrEmpty(randomRestartIntervalInMinsString))
            {
                randomRestartIntervalInMins = int.Parse(randomRestartIntervalInMinsString);
            }

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromMinutes(2 * randomRestartIntervalInMins), null);

            await RestartModules(cts, randomRestartIntervalInMins);
            completed.Set();

            return 0;
        }

        /// <summary>
        /// Restarts random modules periodically (with default restart occurrence once every 5 minutes)
        /// </summary>
        static async Task RestartModules(CancellationTokenSource cts, int randomRestartIntervalInMins)
        {
            Console.WriteLine("Device ID: ", deviceId);
            Console.WriteLine("Module CSV received: ", modulesCSV);
            Console.WriteLine("Random restart interval: ", randomRestartIntervalInMinsString);

            if (moduleNames.Length == 0)
            {
                return;
            }

            ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(connectionString);
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            string payload = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
            Random random = new Random();

            while (!cts.Token.IsCancellationRequested)
            {
                payload = string.Format(payload, moduleNames[random.Next(0, moduleNames.Length)]);
                Console.WriteLine("RestartModule Method Payload: {0}", payload);
                c2dMethod.SetPayloadJson(payload);

                await iotHubServiceClient.InvokeDeviceMethodAsync(deviceId, "$edgeAgent", c2dMethod);

                Thread.Sleep(randomRestartIntervalInMins * 60 * 1000);
            }
        }
    }
}
