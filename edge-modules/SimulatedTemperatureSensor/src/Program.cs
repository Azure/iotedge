// Copyright (c) Microsoft. All rights reserved.

namespace SimulatedTemperatureSensor
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        static readonly Random Rnd = new Random();

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string connectionString = configuration.GetValue<string>("EdgeHubConnectionString");
            var messageDelay = configuration.GetValue<TimeSpan>("MessageDelay", TimeSpan.FromSeconds(5));
            var minTemp = configuration.GetValue<int>("MinTemp", -10);
            var maxTemp = configuration.GetValue<int>("MaxTemp", 40);

            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                // TODO: Remove this when we figure out how to trust the Edge Hub's root CA in modules.
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            ITransportSettings[] settings = { mqttSetting };

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await deviceClient.OpenAsync();

            var cts = new CancellationTokenSource();
            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts); };

            await SendEvent(deviceClient, messageDelay, minTemp, maxTemp, cts);
            return 0;
        }

        static async Task SendEvent(
            DeviceClient moduleClient,
            TimeSpan messageDelay,
            int minTemp,
            int maxTemp,
            CancellationTokenSource cts)
        {
            int count = 1;
            while (!cts.Token.IsCancellationRequested)
            {
                var tempData = new MessageBody
                {
                    Temperature = Rnd.Next(minTemp, maxTemp)
                };

                string dataBuffer = JsonConvert.SerializeObject(tempData);
                var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message: {count}, Body: [{dataBuffer}]");

                await moduleClient.SendEventAsync(eventMessage);
                await Task.Delay(messageDelay, cts.Token);
                count++;
            }
        }

        static void CancelProgram(CancellationTokenSource cts)
        {
            Console.WriteLine("Termination requested, closing.");
            cts.Cancel();
        }

        class MessageBody
        {
            public int Temperature { get; set; }
        }
    }
}