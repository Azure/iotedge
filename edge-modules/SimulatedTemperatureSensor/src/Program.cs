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
            var messageDelay = configuration.GetValue<TimeSpan>("MessageDelay");
            int temperatureThreshold = configuration.GetValue<int>("TemperatureThreshold");

            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                // TODO: Remove this when we figure out how to trust the Edge Hub's root CA in modules.
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            ITransportSettings[] settings = { mqttSetting };

            DeviceClient moduleClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await moduleClient.OpenAsync();

            var cts = new CancellationTokenSource();
            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts); };

            await SendEvent(moduleClient, messageDelay, temperatureThreshold, cts);
            return 0;
        }

        static async Task SendEvent(
            DeviceClient moduleClient,
            TimeSpan messageDelay,
            int temperatureThreshold,
            CancellationTokenSource cts)
        {
            int count = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                int temperature = Rnd.Next(20, 35);
                string dataBuffer = $"{{\"messageId\":{count},\"temperature\":{temperature}}}";
                var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                eventMessage.Properties.Add("temperatureAlert", (temperature > temperatureThreshold) ? "true" : "false");
                Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message: {count}, Data: [{dataBuffer}]");

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
    }
}