// Copyright (c) Microsoft. All rights reserved.
namespace SimulatedTemperatureSensor
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    class Program
    {
        const string MessageCountConfigKey = "MessageCount";
        const string SendDataConfigKey = "SendData";
        const string SendIntervalConfigKey = "SendInterval";

        static readonly ITransientErrorDetectionStrategy DefaultTimeoutErrorDetectionStrategy =
            new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());

        static readonly RetryStrategy DefaultTransientRetryStrategy =
            new ExponentialBackoff(
                5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(4));

        static readonly Guid BatchId = Guid.NewGuid();
        static readonly AtomicBoolean Reset = new AtomicBoolean(false);
        static readonly Random Rnd = new Random();
        static TimeSpan messageDelay;
        static bool sendData = true;

        static ILogger logger = null;
        static IotHubModuleClient staticModuleClient = null;

        public enum ControlCommandEnum
        {
            Reset = 0,
            NoOperation = 1
        }

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine($"{DateTime.UtcNow.ToLogString()} SimulatedTemperatureSensor Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            logger = SetupLogger(configuration);
            messageDelay = configuration.GetValue("MessageDelay", TimeSpan.FromSeconds(5));
            int messageCount = configuration.GetValue(MessageCountConfigKey, 500);
            var simulatorParameters = new SimulatorParameters
            {
                MachineTempMin = configuration.GetValue<double>("machineTempMin", 21),
                MachineTempMax = configuration.GetValue<double>("machineTempMax", 100),
                MachinePressureMin = configuration.GetValue<double>("machinePressureMin", 1),
                MachinePressureMax = configuration.GetValue<double>("machinePressureMax", 10),
                AmbientTemp = configuration.GetValue<double>("ambientTemp", 21),
                HumidityPercent = configuration.GetValue("ambientHumidity", 25)
            };

            logger.LogInformation(
                $"Initializing simulated temperature sensor to send {(SendUnlimitedMessages(messageCount) ? "unlimited" : messageCount.ToString())} "
                + $"messages, at an interval of {messageDelay.TotalSeconds} seconds.\n"
                + $"To change this, set the environment variable {MessageCountConfigKey} to the number of messages that should be sent (set it to -1 to send unlimited messages).");

            string clientTransportType = configuration.GetValue("ClientTransportType", "Amqp_Tcp_Only");

            IotHubModuleClient moduleClient = await CreateModuleClientAsync(
                clientTransportType,
                DefaultTimeoutErrorDetectionStrategy,
                DefaultTransientRetryStrategy);
            await moduleClient.OpenAsync();

            // In v2 SDK, SetDirectMethodCallbackAsync registers a single callback for all methods.
            // We filter by method name inside the callback.
            await moduleClient.SetDirectMethodCallbackAsync(DirectMethodCallback);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), logger);

            TwinProperties currentTwinProperties = await moduleClient.GetTwinPropertiesAsync();
            if (currentTwinProperties.Desired.ContainsKey(SendIntervalConfigKey))
            {
                messageDelay = TimeSpan.FromSeconds((int)currentTwinProperties.Desired[SendIntervalConfigKey]);
            }

            if (currentTwinProperties.Desired.ContainsKey(SendDataConfigKey))
            {
                sendData = (bool)currentTwinProperties.Desired[SendDataConfigKey];
                if (!sendData)
                {
                    logger.LogInformation("Sending data disabled. Change twin configuration to start sending again.");
                }
            }

            staticModuleClient = moduleClient;

            // In v2 SDK, SetDesiredPropertyUpdateCallbackAsync takes Func<PropertyCollection, Task> (no userContext)
            await moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdated);

            // In v2 SDK, SetIncomingMessageCallbackAsync registers a single callback for all incoming messages.
            // We filter by InputName inside the callback.
            await moduleClient.SetIncomingMessageCallbackAsync(IncomingMessageCallback);

            await SendEvents(moduleClient, messageCount, simulatorParameters, cts);
            await cts.Token.WhenCanceled();

            await moduleClient.CloseAsync();
            await moduleClient.DisposeAsync();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            logger.LogInformation("SimulatedTemperatureSensor Main() finished.");
            return 0;
        }

        static ILogger SetupLogger(IConfiguration configuration)
        {
            string logLevel = configuration.GetValue($"{Logger.RuntimeLogLevelEnvKey}", "info");
            Logger.SetLogLevel(logLevel);
            ILogger logger = Logger.Factory.CreateLogger<Program>();
            return logger;
        }

        static bool SendUnlimitedMessages(int maximumNumberOfMessages) => maximumNumberOfMessages < 0;

        static Task<DirectMethodResponse> DirectMethodCallback(DirectMethodRequest methodRequest)
        {
            if (string.Equals(methodRequest.MethodName, "reset", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Received direct method call to reset temperature sensor...");
                Reset.Set(true);
            }

            var response = new DirectMethodResponse((int)HttpStatusCode.OK);
            return Task.FromResult(response);
        }

        // Control Message expected to be:
        // {
        //     "command" : "reset"
        // }
        static Task<MessageAcknowledgement> IncomingMessageCallback(IncomingMessage message)
        {
            if (!string.Equals(message.InputName, "control", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(MessageAcknowledgement.Complete);
            }

            string messageString = Encoding.UTF8.GetString(message.Payload);

            Console.WriteLine($"Received message Body: [{messageString}]");

            try
            {
                var messages = JsonConvert.DeserializeObject<ControlCommand[]>(messageString);

                foreach (ControlCommand messageBody in messages)
                {
                    if (messageBody.Command == ControlCommandEnum.Reset)
                    {
                        Console.WriteLine("Resetting temperature sensor..");
                        Reset.Set(true);
                    }
                }
            }
            catch (JsonSerializationException)
            {
                var messageBody = JsonConvert.DeserializeObject<ControlCommand>(messageString);

                if (messageBody.Command == ControlCommandEnum.Reset)
                {
                    Console.WriteLine("Resetting temperature sensor..");
                    Reset.Set(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Failed to deserialize control command with exception: [{ex}]");
            }

            return Task.FromResult(MessageAcknowledgement.Complete);
        }

        /// <summary>
        /// Module behavior:
        ///        Sends data periodically (with default frequency of 5 seconds).
        ///        Data trend:
        ///         - Machine Temperature regularly rises from 21C to 100C in regularly with jitter
        ///         - Machine Pressure correlates with Temperature 1 to 10psi
        ///         - Ambient temperature stable around 21C
        ///         - Humidity is stable with tiny jitter around 25%
        ///                Method for resetting the data stream.
        /// </summary>
        static async Task SendEvents(
            IotHubModuleClient moduleClient,
            int messageCount,
            SimulatorParameters sim,
            CancellationTokenSource cts)
        {
            int count = 1;
            double currentTemp = sim.MachineTempMin;
            double normal = (sim.MachinePressureMax - sim.MachinePressureMin) / (sim.MachineTempMax - sim.MachineTempMin);

            while (!cts.Token.IsCancellationRequested && (SendUnlimitedMessages(messageCount) || messageCount >= count))
            {
                if (Reset)
                {
                    currentTemp = sim.MachineTempMin;
                    Reset.Set(false);
                }

                if (currentTemp > sim.MachineTempMax)
                {
                    currentTemp += Rnd.NextDouble() - 0.5; // add value between [-0.5..0.5]
                }
                else
                {
                    currentTemp += -0.25 + (Rnd.NextDouble() * 1.5); // add value between [-0.25..1.25] - average +0.5
                }

                if (sendData)
                {
                    var tempData = new MessageBody
                    {
                        Machine = new Machine
                        {
                            Temperature = currentTemp,
                            Pressure = sim.MachinePressureMin + ((currentTemp - sim.MachineTempMin) * normal),
                        },
                        Ambient = new Ambient
                        {
                            Temperature = sim.AmbientTemp + Rnd.NextDouble() - 0.5,
                            Humidity = Rnd.Next(24, 27)
                        },
                        TimeCreated = DateTime.UtcNow
                    };

                    string dataBuffer = JsonConvert.SerializeObject(tempData);
                    var eventMessage = new TelemetryMessage(Encoding.UTF8.GetBytes(dataBuffer));
                    eventMessage.ContentEncoding = "utf-8";
                    eventMessage.ContentType = "application/json";
                    eventMessage.Properties.Add("sequenceNumber", count.ToString());
                    eventMessage.Properties.Add("batchId", BatchId.ToString());
                    logger.LogInformation($"Sending message: {count}, Body: [{dataBuffer}]");
                    try
                    {
                        await moduleClient.SendMessageToRouteAsync("temperatureOutput", eventMessage, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogError($"SendEvents has been canceled, sent {count - 1} messages.");
                        return;
                    }

                    count++;
                }

                try
                {
                    await Task.Delay(messageDelay, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    logger.LogError($"SendEvents has been canceled, sent {count - 1} messages.");
                    return;
                }
            }

            if (messageCount < count)
            {
                logger.LogInformation($"Done sending {messageCount} messages");
            }
        }

        static async Task OnDesiredPropertiesUpdated(PropertyCollection desiredPropertiesPatch)
        {
            // At this point just update the configure configuration.
            if (desiredPropertiesPatch.ContainsKey(SendIntervalConfigKey))
            {
                messageDelay = TimeSpan.FromSeconds((int)desiredPropertiesPatch[SendIntervalConfigKey]);
            }

            if (desiredPropertiesPatch.ContainsKey(SendDataConfigKey))
            {
                bool desiredSendDataValue = (bool)desiredPropertiesPatch[SendDataConfigKey];
                if (desiredSendDataValue != sendData && !desiredSendDataValue)
                {
                    Console.WriteLine("Sending data disabled. Change twin configuration to start sending again.");
                }

                sendData = desiredSendDataValue;
            }

            var patch = new PropertyCollection();
            patch.Add("SendData", sendData);
            patch.Add("SendInterval", messageDelay.TotalSeconds);
            await staticModuleClient.UpdateReportedPropertiesAsync(patch); // Just report back last desired property.
        }

        static async Task<IotHubModuleClient> CreateModuleClientAsync(
            string transportType,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy = null,
            RetryStrategy retryStrategy = null)
        {
            var retryPolicy = new RetryPolicy(transientErrorDetectionStrategy, retryStrategy);
            retryPolicy.Retrying += (_, args) => { logger.LogError($"Retry {args.CurrentRetryCount} times to create module client and failed with exception:{Environment.NewLine}{args.LastException}"); };

            IotHubModuleClient client = await retryPolicy.ExecuteAsync(
                async () =>
                {
                    IotHubClientOptions GetClientOptions()
                    {
                        switch (transportType)
                        {
                            case "Mqtt":
                            case "Mqtt_Tcp_Only":
                                return new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp));
                            case "Mqtt_WebSocket_Only":
                                return new IotHubClientOptions(new IotHubClientMqttSettings(IotHubClientTransportProtocol.WebSocket));
                            case "Amqp_WebSocket_Only":
                                return new IotHubClientOptions(new IotHubClientAmqpSettings(IotHubClientTransportProtocol.WebSocket));
                            default:
                                return new IotHubClientOptions(new IotHubClientAmqpSettings(IotHubClientTransportProtocol.Tcp));
                        }
                    }

                    IotHubClientOptions options = GetClientOptions();
                    logger.LogInformation($"Trying to initialize module client using transport type [{transportType}].");
                    IotHubModuleClient moduleClient = await IotHubModuleClient.CreateFromEnvironmentAsync(options);
                    await moduleClient.OpenAsync();

                    logger.LogInformation($"Successfully initialized module client of transport type [{transportType}].");
                    return moduleClient;
                });

            return client;
        }

        class ControlCommand
        {
            [JsonProperty("command")]
            public ControlCommandEnum Command { get; set; }
        }

        class SimulatorParameters
        {
            public double MachineTempMin { get; set; }

            public double MachineTempMax { get; set; }

            public double MachinePressureMin { get; set; }

            public double MachinePressureMax { get; set; }

            public double AmbientTemp { get; set; }

            public int HumidityPercent { get; set; }
        }
    }
}
