// Copyright (c) Microsoft. All rights reserved.

namespace DirectMethodSender
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Program
    {
        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)}] Main()");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            TimeSpan dmDelay = configuration.GetValue("DMDelay", TimeSpan.FromSeconds(5));

            string targetModuleId = configuration.GetValue("TargetModuleId", "DMReceiver");

            // Get deviced id of this device, exposed as a system variable by the iot edge runtime
            string targetDeviceId = configuration.GetValue<string>("IOTEDGE_DEVICEID");

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);
            Console.WriteLine($"Using transport {transportType.ToString()}");

            ModuleClient moduleClient = await InitModuleClient(transportType);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler)
                = ShutdownHandler.Init(TimeSpan.FromSeconds(5), null);
            await CallDirectMethod(moduleClient, dmDelay, targetDeviceId, targetModuleId, cts).ConfigureAwait(false);
            await moduleClient.CloseAsync();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            return 0;
        }

        static async Task<ModuleClient> InitModuleClient(TransportType transportType)
        {
            ITransportSettings[] GetTransportSettings()
            {
                switch (transportType)
                {
                    case TransportType.Mqtt:
                    case TransportType.Mqtt_Tcp_Only:
                    case TransportType.Mqtt_WebSocket_Only:
                        return new ITransportSettings[] { new MqttTransportSettings(transportType) };
                    default:
                        return new ITransportSettings[] { new AmqpTransportSettings(transportType) };
                }
            }
            ITransportSettings[] settings = GetTransportSettings();

            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings).ConfigureAwait(false);
            await moduleClient.OpenAsync().ConfigureAwait(false);

            Console.WriteLine("Successfully initialized module client.");
            return moduleClient;
        }

        /// <summary>
        /// Module behavior:
        ///        Call HelloWorld Direct Method every 5 seconds.
        /// </summary>
        /// <param name="moduleClient"></param>
        /// <param name="dmDelay"></param>
        /// <param name="targetModuleId"></param>
        /// <param name="cts"></param>
        /// <param name="targetDeviceId"></param>
        /// <returns></returns>
        static async Task CallDirectMethod(
            ModuleClient moduleClient,
            TimeSpan dmDelay,
            string targetDeviceId,
            string targetModuleId, 
            CancellationTokenSource cts)
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Calling Direct Method on module.");

                // Create the request
                var request = new MethodRequest("HelloWorldMethod", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));

                try
                {
                    //Ignore Exception. Keep trying. 
                    MethodResponse response = await moduleClient.InvokeMethodAsync(targetDeviceId, targetModuleId, request);

                    if (response.Status == (int)HttpStatusCode.OK)
                    {
                        await moduleClient.SendEventAsync("AnyOutput", new Message(Encoding.UTF8.GetBytes("Method Call succeeded.")));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                

                await Task.Delay(dmDelay, cts.Token).ConfigureAwait(false);
            }
        }
    }
}
