// Copyright (c) Microsoft. All rights reserved.

namespace TemperatureFilter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Security;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        const string TemperatureThresholdKey = "TemperatureThreshold";
        const int DefaultTemperatureThreshold = 25;
        static int counter;

        static void Main()
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}] Main()");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Mqtt_Tcp_Only);
            Console.WriteLine($"Using transport {transportType.ToString()}");

            InstallCert();
            Init(transportType).Wait();

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
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init(TransportType transportType)
        {
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

            // Open a connection to the Edge runtime
            ModuleClient moduleClient = ModuleClient.CreateFromEnvironment(settings);
            await moduleClient.OpenAsync();
            Console.WriteLine("TemperatureFilter - Opened module client connection");

            ModuleConfig moduleConfig = await GetConfiguration(moduleClient);
            Console.WriteLine($"Using TemperatureThreshold value of {moduleConfig.TemperatureThreshold}");

            var userContext = new Tuple<ModuleClient, ModuleConfig>(moduleClient, moduleConfig);

            // Register callback to be called when a message is sent to "input1"
            await moduleClient.SetInputMessageHandlerAsync(
                "input1",
                PrintAndFilterMessages,
                userContext);
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
                Console.WriteLine($"Missing path to certificate collection file: {certPath}");
                throw new InvalidOperationException("Missing certificate file.");
            }
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate.CreateFromCertFile(certPath)));
            Console.WriteLine("Added Cert: " + certPath);
            store.Close();
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
                await moduleClient.SendEventAsync("alertOutput", filteredMessage);
            }

            return MessageResponse.Completed;
        }

        /// <summary>
        /// Get the configuration for the module (in this case the threshold temperature)s.
        /// </summary>
        static async Task<ModuleConfig> GetConfiguration(ModuleClient moduleClient)
        {
            // First try to get the config from the Module twin
            Twin twin = await moduleClient.GetTwinAsync();
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
