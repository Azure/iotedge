// Copyright (c) Microsoft. All rights reserved.

namespace SimulatedTemperatureSensor
{
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Newtonsoft.Json.Serialization;

    class Program
    {
        static readonly Random Rnd = new Random();
        static AtomicBoolean reset = new AtomicBoolean(false);

        public enum ControlCommandEnum { reset = 0, noop = 1 };

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
            SimulatorParameters sim = new SimulatorParameters()
            {
                MachineTempMin = configuration.GetValue<double>("machineTempMin", 21),
                MachineTempMax = configuration.GetValue<double>("machineTempMax", 100),
                MachinePressureMin = configuration.GetValue<double>("machinePressureMin", 1),
                MachinePressureMax = configuration.GetValue<double>("machinePressureMax", 10),
                AmbientTemp = configuration.GetValue<double>("ambientTemp", 21),
                HumidityPercent = configuration.GetValue<int>("ambientHumidity", 25)
            };

            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                // TODO: Remove this when we figure out how to trust the Edge Hub's root CA in modules.
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            ITransportSettings[] settings = { mqttSetting };

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await deviceClient.OpenAsync();
            await deviceClient.SetMethodHandlerAsync("reset", ResetMethod, null);

            var userContext = deviceClient;
            await deviceClient.SetEventHandlerAsync("control", ControlMessageHandle, userContext);

            var cts = new CancellationTokenSource();
            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts); };

            await SendEvent(deviceClient, messageDelay, sim, cts);
            return 0;
        }

        //TODO: Change this call back once we have the final design for Device Client Acknowledgement.
        //Control Message expected to be:
        // {
        //     "command" : "reset" 
        // } 
        static async Task ControlMessageHandle(Message message, object userContext)
        {
            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            DeviceClient deviceClient = userContext as DeviceClient;

            Console.WriteLine($"Received message Body: [{messageString}]");

            try
            {
                var messageBody = JsonConvert.DeserializeObject<ControlCommand>(messageString);
                if (messageBody.Command == ControlCommandEnum.reset)
                {
                    reset.Set(true);
                }
                else
                {
                    //NoOp
                }
            }
            catch (JsonReaderException ex)
            {
                Console.WriteLine($"Ignoring control message. Wrong control message exception: [{ex.Message}]");
            }

            //TODO: Remove and change to return value (Success or Failure) after Device Client is changed.. 
            await deviceClient.CompleteAsync(message);
        }

        static Task<MethodResponse> ResetMethod(MethodRequest methodRequest, object userContext)
        {
            var response = new MethodResponse((int)HttpStatusCode.OK);
            reset.Set(true);
            return Task.FromResult<MethodResponse>(response);
        }

        static async Task SendEvent(
            DeviceClient deviceClient,
            TimeSpan messageDelay,
            SimulatorParameters sim,
            CancellationTokenSource cts)
        {
            int count = 1;
            double currentTemp = sim.MachineTempMin;
            double normal = (sim.MachinePressureMax - sim.MachinePressureMin) / (sim.MachineTempMax - sim.MachineTempMin);

            while (!cts.Token.IsCancellationRequested)
            {
                if (reset)
                {
                    currentTemp = sim.MachineTempMin;
                    reset.Set(false);
                }
                if (currentTemp > sim.MachineTempMax)
                {
                    currentTemp += Rnd.NextDouble() - 0.5; // add value between [-0.5..0.5]
                }
                else
                {
                    currentTemp += -0.25 + (Rnd.NextDouble() * 1.5); // add value between [-0.25..1.25] - average +0.5
                }

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
                    TimeCreated = DateTime.UtcNow.ToString("o")
                };

                string dataBuffer = JsonConvert.SerializeObject(tempData);
                var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message: {count}, Body: [{dataBuffer}]");

                await deviceClient.SendEventAsync("temperatureOutput", eventMessage);
                await Task.Delay(messageDelay, cts.Token);
                count++;
            }
        }

        static void CancelProgram(CancellationTokenSource cts)
        {
            Console.WriteLine("Termination requested, closing.");
            cts.Cancel();
        }

        public class ControlCommand
        {
            [JsonProperty("command")]
            public ControlCommandEnum Command { get; set; }
        }

        class MessageBody
        {
            [JsonProperty(PropertyName = "machine")]
            public Machine Machine { get; set; }

            [JsonProperty(PropertyName = "ambient")]
            public Ambient Ambient { get; set; }

            [JsonProperty(PropertyName = "timeCreated")]
            public string TimeCreated { get; set; }
        }

        class Machine
        {
            [JsonProperty(PropertyName ="temperature")]
            public double Temperature { get; set; }

            [JsonProperty(PropertyName = "pressure")]
            public double Pressure { get; set; }
        }

        class Ambient
        {
            [JsonProperty(PropertyName = "temperature")]
            public double Temperature { get; set; }

            [JsonProperty(PropertyName = "humidity")]
            public int Humidity { get; set; }
        }

        internal class SimulatorParameters
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
