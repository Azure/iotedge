// Copyright (c) Microsoft. All rights reserved.

namespace SimulatedTemperatureSensor
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    class Program
    {
        static readonly Random Rnd = new Random();
        static readonly AtomicBoolean Reset = new AtomicBoolean(false);

        public enum ControlCommandEnum { Reset = 0, Noop = 1 };

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            InstallCert();

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string connectionString = configuration.GetValue<string>("EdgeHubConnectionString");
            TimeSpan messageDelay = configuration.GetValue("MessageDelay", TimeSpan.FromSeconds(5));
            var sim = new SimulatorParameters()
            {
                MachineTempMin = configuration.GetValue<double>("machineTempMin", 21),
                MachineTempMax = configuration.GetValue<double>("machineTempMax", 100),
                MachinePressureMin = configuration.GetValue<double>("machinePressureMin", 1),
                MachinePressureMax = configuration.GetValue<double>("machinePressureMax", 10),
                AmbientTemp = configuration.GetValue<double>("ambientTemp", 21),
                HumidityPercent = configuration.GetValue("ambientHumidity", 25)
            };

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Mqtt_Tcp_Only);
            Console.WriteLine($"Using transport {transportType.ToString()}");

            var mqttSetting = new MqttTransportSettings(transportType);
            // Pin root certificate from file at runtime on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // Terminate on errors other than those caused by a chain failure
                    SslPolicyErrors terminatingErrors = sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors;
                    if (terminatingErrors != SslPolicyErrors.None)
                    {
                        Console.WriteLine("Discovered SSL session errors: {0}", terminatingErrors);
                        return false;
                    }

                    // Load the expected root certificate
                    string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
                    if (string.IsNullOrWhiteSpace(certPath))
                    {
                        Console.WriteLine("Missing path to the root certificate file.");
                        return false;
                    }
                    else if (!File.Exists(certPath))
                    {
                        Console.WriteLine($"Unable to find a root certificate file at {certPath}.");
                        return false;
                    }
                    var expectedRoot = new X509Certificate2(certPath);

                    // Allow the chain the chance to rebuild itself with the expected root
                    chain.ChainPolicy.ExtraStore.Add(expectedRoot);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    if (!chain.Build(new X509Certificate2(certificate)))
                    {
                        Console.WriteLine("Unable to build the chain using the expected root certificate.");
                        return false;
                    }

                    // Pin the trusted root of the chain to the expected root certificate
                    X509Certificate2 actualRoot = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                    if (!expectedRoot.Equals(actualRoot))
                    {
                        Console.WriteLine("The certificate chain was not signed by the trusted root certificate.");
                        return false;
                    }
                    return true;
                };
            }
            ITransportSettings[] settings = { mqttSetting };

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await deviceClient.OpenAsync();
            await deviceClient.SetMethodHandlerAsync("reset", ResetMethod, null);

            DeviceClient userContext = deviceClient;
            await deviceClient.SetInputMessageHandlerAsync("control", ControlMessageHandle, userContext);

            var cts = new CancellationTokenSource();
            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts); };

            await SendEvent(deviceClient, messageDelay, sim, cts);
            return 0;
        }

        /// <summary>
        /// Add certificate in local cert store for use by client for secure connection to IoT Edge runtime
        /// </summary>
        static void InstallCert()
        {
            // Suppress cert validation on Windows for now
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (string.IsNullOrWhiteSpace(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing path to certificate file.");
            }
            else if (!File.Exists(certPath))
            {
                // We cannot proceed further without a proper cert file
                Console.WriteLine($"Missing certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate.CreateFromCertFile(certPath)));
            Console.WriteLine("Added Cert: " + certPath);
            store.Close();
        }

        //Control Message expected to be:
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
                    else
                    {
                        //NoOp
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize control command with exception: [{ex.Message}]");
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
