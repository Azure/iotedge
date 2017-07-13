
namespace TemperatureFilter
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    class Program
    {
        const string TemperatureThresholdKey = "TemperatureThreshold";
        static int counter;

        static void Main(string[] args)
        {
            // The Edge runtime gives us the connection string we need -- it is injected as an environment variable
            string connectionString = Environment.GetEnvironmentVariable("EdgeHubConnectionString");
            Init(connectionString).Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive messages containing temperature information
        /// </summary>
        static async Task Init(string connectionString)
        {
            // Open a connection to the runtime
            ITransportSettings[] settings =
            {
                new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
                { RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true }
            };
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, settings);
            await deviceClient.OpenAsync();
            Console.WriteLine("TemperatureFilter - Opened module client connection");

            ModuleConfig moduleModuleConfig = await GetConfiguration(deviceClient);

            var userContext = new Tuple<DeviceClient, ModuleConfig>(deviceClient, moduleModuleConfig);
            // Register callback to be called when a message is sent to "input1"
            await deviceClient.SetEventHandlerAsync("input1", PrintAndFilterMessages, userContext);
        }

        /// <summary>
        /// This method is called whenever the Filter module is sent a message from the EdgeHub. 
        /// It filters the messages based on the temperature value in the body of the messages, 
        /// and the temperature threshold set via config.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task PrintAndFilterMessages(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var userContextValues = userContext as Tuple<DeviceClient, ModuleConfig>;
            if (userContextValues == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain expected values");
            }
            DeviceClient deviceClient = userContextValues.Item1;
            ModuleConfig moduleModuleConfig = userContextValues.Item2;

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            // Get message body, containing the Temperature data
            var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

            if (messageBody != null && messageBody.Temperature > moduleModuleConfig.TemperatureThreshold)
            {
                Console.WriteLine($"Temperature {messageBody.Temperature} exceeds threshold {moduleModuleConfig.TemperatureThreshold}");
                var filteredMessage = new Message(messageBytes);
                foreach (KeyValuePair<string, string> prop in message.Properties)
                {
                    filteredMessage.Properties.Add(prop.Key, prop.Value);
                }

                filteredMessage.Properties.Add("MessageType", "Alert");
                await deviceClient.SendEventAsync("alertOutput", filteredMessage);
            }
        }

        /// <summary>
        /// Get the configuration for the module (in this case the threshold temperature)s. 
        /// </summary>
        static async Task<ModuleConfig> GetConfiguration(DeviceClient deviceClient)
        {
            // First try to get the config from the Module twin
            Twin twin = await deviceClient.GetTwinAsync();
            int tempThreshold = 0;
            if (twin.Properties.Desired.Contains(TemperatureThresholdKey))
            {
                tempThreshold = (int)twin.Properties.Desired[TemperatureThresholdKey];
            }
            // Else try to get it from the environment variables.
            else
            {
                string tempThresholdEnvVar = Environment.GetEnvironmentVariable(TemperatureThresholdKey);
                if (!string.IsNullOrWhiteSpace(tempThresholdEnvVar))
                {
                    int.TryParse(tempThresholdEnvVar, out tempThreshold);
                }
            }

            // If everything else fails, set it to default.
            if (tempThreshold == 0)
            {
                tempThreshold = 25; // Default value
            }
            return new ModuleConfig(tempThreshold);
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

        /// <summary>
        /// The class containing the expected schema for the body of the incoming message.
        /// </summary>
        class MessageBody
        {
            public int Temperature { get; set; }
        }
    }
}
