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

            var connectionString = configuration.GetValue<string>("CONNECTION_STRING");

            var messageDelay = configuration.GetValue<TimeSpan>("MESSAGE_DELAY");

            var temperatureThreshold = configuration.GetValue<int>("TEMPERATURE_THRESHOLD");

            var moduleId = configuration.GetValue<string>("MODULE_ID");

            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            ITransportSettings[] settings = { mqttSetting };

            var moduleClientInstance = ModuleClient.CreateFromConnectionString(connectionString, settings);
            await moduleClientInstance.OpenAsync();

            var cts = new CancellationTokenSource();
            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts); };

            await SendEvent(moduleClientInstance, messageDelay, temperatureThreshold, moduleId, cts);

            return 0;
        }

        static async Task SendEvent(ModuleClient moduleClient, TimeSpan messageDelay, int temperatureThreshold, string moduleId, CancellationTokenSource cts)
        {
            int count=0;
            while (!cts.Token.IsCancellationRequested)
            {
                var temperature = Rnd.Next(20, 35);
                var dataBuffer = string.Format("{{\"moduleId\":\"{0}\",\"messageId\":{1},\"temperature\":{2}}}", moduleId, count, temperature);
                var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                eventMessage.Properties.Add("temperatureAlert", (temperature > temperatureThreshold) ? "true" : "false");
                Console.WriteLine("\t{0}> Sending message: {1}, Data: [{2}]", DateTime.Now.ToLocalTime(), count, dataBuffer);

                await moduleClient.SendMessageAsync(eventMessage);
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

    public static class ModuleUtils
    {
        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}