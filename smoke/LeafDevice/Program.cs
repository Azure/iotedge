// Copyright (c) Microsoft. All rights reserved.

namespace LeafDevice
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    [Command(
        Name = "LeafDevice",
        Description = "An app which installs a certificate into cert store, creates a device and sends message and check if the message was received. This is to be used on a transparent Gateway setup for testing.",
        ExtendedHelpText = @"
Environment Variables:
  Most options in this command override environment variables. In other words,
  the value of the corresponding environment variable will be used unless the
  option is specified on the command line.

  Option                    Environment variable
  --connection-string       iothubConnectionString
  --eventhub-endpoint       eventhubCompatibleEndpointWithEntityPath

Defaults:
  All options to this command have defaults. If an option is not specified and
  its corresponding environment variable is not defined, then the default will
  be used.

  Option                    Default value
  --connection-string       get the value from Key Vault
  --eventhub-endpoint       get the value from Key Vault
  --device-id               an auto-generated unique identifier
  --certificate             Empty String.
  --edge-hostname           Empty String.
"
        )]
    [HelpOption]
    class Program
    {
        // ReSharper disable once UnusedMember.Local
        static int Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args).Result;

        [Option("-c|--connection-string <value>", Description = "Device connection string (hub-scoped, e.g. iothubowner)")]
        public string DeviceConnectionString { get; } = Environment.GetEnvironmentVariable("iothubConnectionString");

        [Option("-e|--eventhub-endpoint <value>", Description = "Event Hub-compatible endpoint for IoT Hub, including EntityPath")]
        public string EventHubCompatibleEndpointWithEntityPath { get; } = Environment.GetEnvironmentVariable("eventhubCompatibleEndpointWithEntityPath");

        [Option("-ct|--certificate <value>", Description = "Certificate file to be installed on the machine.")]
        public string CertificateFileName { get; } = "";

        [Option("-d|--device-id", Description = "Leaf device identifier to be registered with IoT Hub")]
        public string DeviceId { get; } = $"leaf-device--{Guid.NewGuid()}";

        [Option("-ed|--edge-hostname", Description = "Leaf device identifier to be registered with IoT Hub")]
        public string EdgeHostName { get; } = "";

        [Option("--use-web-sockets", CommandOptionType.NoValue, Description = "Use websockets for IoT Hub connections.")]
        public bool UseWebSockets { get; } = false;

        // ReSharper disable once UnusedMember.Local
        async Task<int> OnExecuteAsync()
        {
            try
            {
                string connectionString = this.DeviceConnectionString ??
                    await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

                string endpoint = this.EventHubCompatibleEndpointWithEntityPath ??
                    await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey");

                var test = new LeafDevice(
                    connectionString,
                    endpoint,
                    this.DeviceId,
                    this.CertificateFileName,
                    this.EdgeHostName,
                    this.UseWebSockets);
                await test.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }

            Console.WriteLine("Success!");
            return 0;
        }
    }
}
