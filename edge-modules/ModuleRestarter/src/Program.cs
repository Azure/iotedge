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
        static readonly string ConnectionString = Environment.GetEnvironmentVariable("IoTHubConnectionString");
        static readonly string DeviceId = Environment.GetEnvironmentVariable("DeviceId");
        static readonly string ModulesCSV = Environment.GetEnvironmentVariable("DesiredModulesToRestartCSV");
        static readonly string RandomRestartIntervalInMinsString = Environment.GetEnvironmentVariable("RandomRestartIntervalInMins");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            if (string.IsNullOrEmpty(ModulesCSV))
            {
                return 0;
            }

            int randomRestartIntervalInMins = 5;
            if (!string.IsNullOrEmpty(RandomRestartIntervalInMinsString))
            {
                randomRestartIntervalInMins = int.Parse(RandomRestartIntervalInMinsString);
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
            Console.WriteLine("Device ID: {0}", DeviceId);
            Console.WriteLine("Module CSV received: {0}", ModulesCSV);
            Console.WriteLine("Random restart interval: {0}", RandomRestartIntervalInMinsString);

            string[] moduleNames = ModulesCSV.Split(",");

            ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(ConnectionString);
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            string payload = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
            Random random = new Random();

            while (!cts.Token.IsCancellationRequested)
            {
                payload = string.Format(payload, moduleNames[random.Next(0, moduleNames.Length)]);
                Console.WriteLine("RestartModule Method Payload: {0}", payload);
                c2dMethod.SetPayloadJson(payload);

                await iotHubServiceClient.InvokeDeviceMethodAsync(DeviceId, "$edgeAgent", c2dMethod);

                Thread.Sleep(randomRestartIntervalInMins * 60 * 1000);
            }
        }
    }
}
