
namespace TemperatureFilter
{
    using System;
	using System.IO;
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
        const int DefaultTemperatureThreshold = 25;
        static int counter;

        static void Main(string[] args)
        {
            // The Edge runtime gives us the connection string we need -- it is injected as an environment variable
            string connectionString =
                Environment.GetEnvironmentVariable("EdgeHubConnectionString");
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
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init(string connectionString)
        {
            // Use Mqtt transport settings.
            string caCertFilePath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            if ((caCertFilePath == null) || (!File.Exists(caCertFilePath))) {
                // there is no CA cert provided, bypass cert verification
                Console.WriteLine("Bypassing Certificate Validation.");
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            ITransportSettings[] settings = { mqttSetting };

            Console.WriteLine("Connection String {0}", connectionString);

            // Open a connection to the Edge runtime
            DeviceClient deviceClient =
                DeviceClient.CreateFromConnectionString(connectionString, settings);
            await deviceClient.OpenAsync();
            Console.WriteLine("TemperatureFilter - Opened module client connection");

            ModuleConfig moduleConfig = await GetConfiguration(deviceClient);
            Console.WriteLine($"Using TemperatureThreshold value of {moduleConfig.TemperatureThreshold}");

            var userContext = new Tuple<DeviceClient, ModuleConfig>(deviceClient, moduleConfig);

            // Register callback to be called when a message is sent to "input1"
            await deviceClient.SetInputMessageHandlerAsync(
                "input1",
                PrintAndFilterMessages,
                userContext);
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

            var userContextValues = userContext as Tuple<DeviceClient, ModuleConfig>;
            if (userContextValues == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " +
                    "expected values");
            }
            DeviceClient deviceClient = userContextValues.Item1;
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
                await deviceClient.SendEventAsync("alertOutput", filteredMessage);
            }

            return MessageResponse.Completed;
        }

        /// <summary>
        /// Get the configuration for the module (in this case the threshold temperature)s.
        /// </summary>
        static async Task<ModuleConfig> GetConfiguration(DeviceClient deviceClient)
        {
            // First try to get the config from the Module twin
            Twin twin = await deviceClient.GetTwinAsync();
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
