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
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        const string MessageCountConfigKey = "MessageCount";
        const string SendDataConfigKey = "SendData";
        const string SendIntervalConfigKey = "SendInterval";

        static readonly Guid BatchId = Guid.NewGuid();
        static readonly AtomicBoolean Reset = new AtomicBoolean(false);
        static readonly Random Rnd = new Random();
        static TimeSpan messageDelay;
        static bool sendData = true;

        public enum ControlCommandEnum
        {
            Reset = 0,
            NoOperation = 1
        }

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine("SimulatedTemperatureSensor Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

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

            Console.WriteLine(
                $"Initializing simulated temperature sensor to send {(SendUnlimitedMessages(messageCount) ? "unlimited" : messageCount.ToString())} "
                + $"messages, at an interval of {messageDelay.TotalSeconds} seconds.\n"
                + $"To change this, set the environment variable {MessageCountConfigKey} to the number of messages that should be sent (set it to -1 to send unlimited messages).");

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);

            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                transportType,
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy);
            await moduleClient.OpenAsync();
            await moduleClient.SetMethodHandlerAsync("reset", ResetMethod, null);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), null);

            Twin currentTwinProperties = await moduleClient.GetTwinAsync();
            //currentTwinProperties.Properties.Desired.Version
            //if (currentTwinProperties.Properties.Desired.Contains(SendIntervalConfigKey))
            //{
            //    messageDelay = TimeSpan.FromSeconds((int)currentTwinProperties.Properties.Desired[SendIntervalConfigKey]);
            //}

            //if (currentTwinProperties.Properties.Desired.Contains(SendDataConfigKey))
            //{
            //    sendData = (bool)currentTwinProperties.Properties.Desired[SendDataConfigKey];
            //    if (!sendData)
            //    {
            //        Console.WriteLine("Sending data disabled. Change twin configuration to start sending again.");
            //    }
            //}

            ModuleClient userContext = moduleClient;
            await moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdated, userContext);
            await moduleClient.SetInputMessageHandlerAsync("control", ControlMessageHandle, userContext);
            await SendEvents(moduleClient, messageCount, simulatorParameters, cts);
            await cts.Token.WhenCanceled();

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Console.WriteLine("SimulatedTemperatureSensor Main() finished.");
            return 0;
        }

        static bool SendUnlimitedMessages(int maximumNumberOfMessages) => maximumNumberOfMessages < 0;

        // Control Message expected to be:
        // {
        //     "command" : "reset"
        // }
        static Task<MessageResponse> ControlMessageHandle(Message message, object userContext)
        {
            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);

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

            return Task.FromResult(MessageResponse.Completed);
        }

        static Task<MethodResponse> ResetMethod(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Received direct method call to reset temperature sensor...");
            Reset.Set(true);
            var response = new MethodResponse((int)HttpStatusCode.OK);
            return Task.FromResult(response);
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
            ModuleClient moduleClient,
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
                    var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                    eventMessage.Properties.Add("sequenceNumber", count.ToString());
                    eventMessage.Properties.Add("batchId", BatchId.ToString());
                    Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message: {count}, Body: [{dataBuffer}]");

                    await moduleClient.SendEventAsync("temperatureOutput", eventMessage);
                    count++;
                }

                await Task.Delay(messageDelay, cts.Token);
            }

            if (messageCount < count)
            {
                Console.WriteLine($"Done sending {messageCount} messages");
            }
        }

        static async Task OnDesiredPropertiesUpdated(TwinCollection desiredPropertiesPatch, object userContext)
        {
            // At this point just update the configure configuration.
            Console.WriteLine(desiredPropertiesPatch.ToJson());
            if (desiredPropertiesPatch.Contains(SendIntervalConfigKey))
            {
                messageDelay = TimeSpan.FromSeconds((int)desiredPropertiesPatch[SendIntervalConfigKey]);
            }

            if (desiredPropertiesPatch.Contains(SendDataConfigKey))
            {
                bool desiredSendDataValue = (bool)desiredPropertiesPatch[SendDataConfigKey];
                if (desiredSendDataValue != sendData && !desiredSendDataValue)
                {
                    Console.WriteLine("Twin update sending data disabled. Change twin configuration to start sending again.");
                }

                sendData = desiredSendDataValue;
            }

            var moduleClient = (ModuleClient)userContext;
            var patch = new TwinCollection($"{{ \"SendData\":{sendData.ToString().ToLower()}, \"SendInterval\": {messageDelay.TotalSeconds}}}");
            await moduleClient.UpdateReportedPropertiesAsync(patch); // Just report back last desired property.
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
