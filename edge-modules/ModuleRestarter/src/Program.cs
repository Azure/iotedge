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
        static readonly int RandomRestartIntervalInMins = int.Parse(Environment.GetEnvironmentVariable("RandomRestartIntervalInMins"));

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            if (string.IsNullOrEmpty(ModulesCSV))
            {
                Console.WriteLine("No modules specified to restart. Stopping.");
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
            Console.WriteLine("Device ID: {0}", DeviceId);
            Console.WriteLine("Module CSV received: {0}", ModulesCSV);
            Console.WriteLine("Random restart interval: {0}", RandomRestartIntervalInMins);

            string[] moduleNames = ModulesCSV.Split(",");
            ServiceClient iotHubServiceClient = ServiceClient.CreateFromConnectionString(ConnectionString);
            CloudToDeviceMethod c2dMethod = new CloudToDeviceMethod("RestartModule");
            Random random = new Random();

            while (!cts.Token.IsCancellationRequested)
            {
                string payload = "{{ \"SchemaVersion\": \"1.0\", \"Id\": \"{0}\" }}";
                payload = string.Format(payload, moduleNames[random.Next(0, moduleNames.Length)]);
                Console.WriteLine("RestartModule Method Payload: {0}", payload);
                c2dMethod.SetPayloadJson(payload);

                await iotHubServiceClient.InvokeDeviceMethodAsync(DeviceId, "$edgeAgent", c2dMethod);

                Thread.Sleep(RandomRestartIntervalInMins * 60 * 1000);
            }
        }
    }
}
