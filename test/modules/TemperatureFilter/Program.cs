// Copyright (c) Microsoft. All rights reserved.
namespace TemperatureFilter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class Program
    {
        const string TemperatureThresholdKey = "TemperatureThreshold";
        const int DefaultTemperatureThreshold = 25;

        static readonly ILogger Logger = ModuleUtil.CreateLogger("TemperatureFilter");
        static int counter;

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger.LogInformation("TemperatureFilter Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);

            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                transportType,
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                Logger);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            ModuleConfig moduleConfig = await GetConfigurationAsync(moduleClient);
            Logger.LogInformation($"Using TemperatureThreshold value of {moduleConfig.TemperatureThreshold}");

            var userContext = Tuple.Create(moduleClient, moduleConfig);
            await moduleClient.SetInputMessageHandlerAsync("input1", PrintAndFilterMessages, userContext);

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("TemperatureFilter Main() finished.");
            return 0;
        }

        /// <summary>
        /// This method is called whenever the Filter module is sent a message from the EdgeHub.
        /// It filters the messages based on the temperature value in the body of the messages,
        /// and the temperature threshold set via config.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PrintAndFilterMessages(Message message, object userContext)
        {
            try
            {
                int counterValue = Interlocked.Increment(ref counter);

                var userContextValues = userContext as Tuple<ModuleClient, ModuleConfig>;
                if (userContextValues == null)
                {
                    throw new InvalidOperationException("UserContext doesn't contain expected values");
                }

                ModuleClient moduleClient = userContextValues.Item1;
                ModuleConfig moduleConfig = userContextValues.Item2;

                byte[] messageBytes = message.GetBytes();
                string messageString = Encoding.UTF8.GetString(messageBytes);
                Logger.LogInformation($"Received message: {counterValue}, Body: [{messageString}]");

                // Get message body, containing the Temperature data
                var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

                if (messageBody != null
                    && messageBody.Machine.Temperature > moduleConfig.TemperatureThreshold)
                {
                    Logger.LogInformation($"Temperature {messageBody.Machine.Temperature} exceeds threshold {moduleConfig.TemperatureThreshold}");
                    var filteredMessage = new Message(messageBytes);
                    foreach (KeyValuePair<string, string> prop in message.Properties)
                    {
                        filteredMessage.Properties.Add(prop.Key, prop.Value);
                    }

                    filteredMessage.Properties.Add("MessageType", "Alert");
                    await moduleClient.SendEventAsync("alertOutput", filteredMessage);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in PrintAndFilterMessages: {e}");
            }

            return MessageResponse.Completed;
        }

        /// <summary>
        /// Get the configuration for the module (in this case the threshold temperature)s.
        /// </summary>
        static async Task<ModuleConfig> GetConfigurationAsync(ModuleClient moduleClient)
        {
            // First try to get the config from the Module twin
            Twin twin = await moduleClient.GetTwinAsync();
            if (twin.Properties.Desired.Contains(TemperatureThresholdKey))
            {
                int tempThreshold = (int)twin.Properties.Desired[TemperatureThresholdKey];
                return new ModuleConfig(tempThreshold);
            }
            else
            {
                // Else try to get it from the environment variables.
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
