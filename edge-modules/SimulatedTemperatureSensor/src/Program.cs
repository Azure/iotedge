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

            string caCertFilePath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            if ((caCertFilePath == null) || (!File.Exists(caCertFilePath))) {
                // there is no CA cert provided, bypass cert verification
                Console.WriteLine("Bypassing Certificate Validation.");
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            ITransportSettings[] settings = { mqttSetting };

            Console.WriteLine("Connection String {0}", connectionString);
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await deviceClient.OpenAsync();
            await deviceClient.SetMethodHandlerAsync("reset", ResetMethod, null);

            var userContext = deviceClient;
            await deviceClient.SetInputMessageHandlerAsync("control", ControlMessageHandle, userContext);

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
        static Task<MessageResponse> ControlMessageHandle(Message message, object userContext)
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

            return Task.FromResult(MessageResponse.Completed);
        }

        static Task<MethodResponse> ResetMethod(MethodRequest methodRequest, object userContext)
        {
            var response = new MethodResponse((int)HttpStatusCode.OK);
            reset.Set(true);
            return Task.FromResult<MethodResponse>(response);
        }

        /// <summary>
        /// Module behavior:
        ///        Sends data once every 5 seconds.
        ///        Data trend:
        ///-	Machine Temperature regularly rises from 21C to 100C in regularly with jitter
        ///-	Machine Pressure correlates with Temperature 1 to 10psi
        ///-	Ambient temperature stable around 21C
        ///-	Humidity is stable with tiny jitter around 25%
        ///                Method for resetting the data stream
        /// </summary>
        /// <param name="deviceClient"></param>
        /// <param name="messageDelay"></param>
        /// <param name="sim"></param>
        /// <param name="cts"></param>
        /// <returns></returns>
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
                    TimeCreated = DateTime.UtcNow
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
