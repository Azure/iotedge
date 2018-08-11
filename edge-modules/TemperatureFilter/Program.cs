// Copyright (c) Microsoft. All rights reserved.

namespace TemperatureFilter
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    class Program
    {
        const int RetryCount = 5;
        static readonly ITransientErrorDetectionStrategy TimeoutErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        const string TemperatureThresholdKey = "TemperatureThreshold";
        const int DefaultTemperatureThreshold = 25;
        static int counter;

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)}] Main()");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);
            Console.WriteLine($"Using transport {transportType.ToString()}");

            var retryPolicy = new RetryPolicy(TimeoutErrorDetectionStrategy, TransientRetryStrategy);
            retryPolicy.Retrying += (_, args) =>
            {
                Console.WriteLine($"Creating ModuleClient failed with exception {args.LastException}");
                if (args.CurrentRetryCount < RetryCount)
                {
                    Console.WriteLine("Retrying...");
                }
            };
            Tuple<ModuleClient, ModuleConfig> moduleclientAndConfig = await retryPolicy.ExecuteAsync(() => InitModuleClient(transportType));

            Tuple<ModuleClient, ModuleConfig> userContext = moduleclientAndConfig;



            await moduleclientAndConfig.Item1.SetInputMessageHandlerAsync("input1", PrintAndFilterMessages, userContext).ConfigureAwait(false);

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
            return 0;
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        static async Task<Tuple<ModuleClient, ModuleConfig>> InitModuleClient(TransportType transportType)
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
            Console.WriteLine("TemperatureFilter - Opened module client connection");

            ModuleConfig moduleConfig = await GetConfiguration(moduleClient).ConfigureAwait(false);
            Console.WriteLine($"Using TemperatureThreshold value of {moduleConfig.TemperatureThreshold}");

            Console.WriteLine("Successfully initialized module client.");
            return new Tuple<ModuleClient, ModuleConfig>(moduleClient, moduleConfig);
        }

        /// <summary>
        /// This method is called whenever the Filter module is sent a message from the EdgeHub.
        /// It filters the messages based on the temperature value in the body of the messages,
        /// and the temperature threshold set via config.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PrintAndFilterMessages(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var userContextValues = userContext as Tuple<ModuleClient, ModuleConfig>;
            if (userContextValues == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " +
                    "expected values");
            }
            ModuleClient moduleClient = userContextValues.Item1;
            ModuleConfig moduleModuleConfig = userContextValues.Item2;

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            // Get message body, containing the Temperature data
            var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

            if (messageBody != null
                && messageBody.Machine.Temperature > moduleModuleConfig.TemperatureThreshold)
            {
                Console.WriteLine($"Temperature {messageBody.Machine.Temperature} " +
                    $"exceeds threshold {moduleModuleConfig.TemperatureThreshold}");
                var filteredMessage = new Message(messageBytes);
                foreach (KeyValuePair<string, string> prop in message.Properties)
                {
                    filteredMessage.Properties.Add(prop.Key, prop.Value);
                }

                filteredMessage.Properties.Add("MessageType", "Alert");
                await moduleClient.SendEventAsync("alertOutput", filteredMessage).ConfigureAwait(false);
            }

            return MessageResponse.Completed;
        }

        /// <summary>
        /// Get the configuration for the module (in this case the threshold temperature)s.
        /// </summary>
        static async Task<ModuleConfig> GetConfiguration(ModuleClient moduleClient)
        {
            // First try to get the config from the Module twin
            Twin twin = await moduleClient.GetTwinAsync().ConfigureAwait(false);
            if (twin.Properties.Desired.Contains(TemperatureThresholdKey))
            {
                int tempThreshold = (int)twin.Properties.Desired[TemperatureThresholdKey];
                return new ModuleConfig(tempThreshold);
            }
            // Else try to get it from the environment variables.
            else
            {
                string tempThresholdEnvVar = Environment.GetEnvironmentVariable(TemperatureThresholdKey);
                if (!string.IsNullOrWhiteSpace(tempThresholdEnvVar) && int.TryParse(tempThresholdEnvVar, out int tempThreshold))
                {
                    return new ModuleConfig(tempThreshold);
                }
            }

            // If config wasn't set in either Twin or Environment variables, use default.
            return new ModuleConfig(DefaultTemperatureThreshold);
        }

        /// <summary>
        /// This class contains the configuration for this module. In this case, it is just the temperature threshold.
        /// </summary>
        class ModuleConfig
        {
            public ModuleConfig(int temperatureThreshold)
            {
                this.TemperatureThreshold = temperatureThreshold;
            }

            public int TemperatureThreshold { get; }
        }

    }
}
