// Copyright (c) Microsoft. All rights reserved.
namespace LeafDevice
{
    using System;
    using System.Collections.Generic;
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

        [Option("-ct|--certificate <value>", Description = "Trust bundle CA Certificate(s) file to be installed on the machine.")]
        public string TrustedCACertificateFileName { get; } = "";

        [Option("-d|--device-id", Description = "Leaf device identifier to be registered with IoT Hub")]
        public string DeviceId { get; } = $"leaf-device--{Guid.NewGuid()}";

        [Option("-ed|--edge-hostname", Description = "Leaf device identifier to be registered with IoT Hub")]
        public string EdgeHostName { get; } = "";

        [Option("--use-web-sockets", CommandOptionType.NoValue, Description = "Use websockets for IoT Hub connections.")]
        public bool UseWebSockets { get; } = false;

        [Option("-cac|--x509-ca-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format to be used for X.509 CA authentication.")]
        public string X509CACertPath { get; } = "";

        [Option("-cak|--x509-ca-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format to be used for X.509 CA authentication.")]
        public string X509CAKeyPath { get; } = "";

        [Option("-ctpc|--x509-primary-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format. This is needed for thumbprint auth and used as the primary certificate.")]
        public string X509PrimaryCertPath { get; } = "";

        [Option("-ctpk|--x509-primary-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format. This is needed for thumbprint auth and used as the primary certificate's key.")]
        public string X509PrimaryKeyPath { get; } = "";

        [Option("-ctsc|--x509-secondary-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format. This is needed for thumbprint auth and used as the secondary certificate.")]
        public string X509SecondaryCertPath { get; } = "";

        [Option("-ctsk|--x509-secondary-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format. This is needed for thumbprint auth and used as the secondary certificate's key.")]
        public string X509SecondaryKeyPath { get; } = "";

        // ReSharper disable once UnusedMember.Local
        async Task<int> OnExecuteAsync()
        {
            try
            {
                string connectionString = this.DeviceConnectionString ??
                    await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

                string endpoint = this.EventHubCompatibleEndpointWithEntityPath ??
                    await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey");

                if (!string.IsNullOrWhiteSpace(X509PrimaryCertPath) &&
                    !string.IsNullOrWhiteSpace(X509PrimaryKeyPath) &&
                    !string.IsNullOrWhiteSpace(X509SecondaryCertPath) &&
                    !string.IsNullOrWhiteSpace(X509SecondaryKeyPath))
                {
                    // use thumbprint auth and perform test for both primary and secondary certificates
                    var thumbprintCerts = new List<string>() { this.X509PrimaryCertPath, this.X509SecondaryCertPath };
                    var testPrimary = new LeafDevice(
                        connectionString,
                        endpoint,
                        this.DeviceId,
                        this.TrustedCACertificateFileName,
                        this.EdgeHostName,
                        this.UseWebSockets,
                        this.X509PrimaryCertPath,
                        this.X509PrimaryKeyPath,
                        thumbprintCerts);
                    await testPrimary.RunAsync();

                    var testSeondary = new LeafDevice(
                        connectionString,
                        endpoint,
                        this.DeviceId,
                        this.TrustedCACertificateFileName,
                        this.EdgeHostName,
                        this.UseWebSockets,
                        this.X509PrimaryCertPath,
                        this.X509PrimaryKeyPath,
                        thumbprintCerts);
                    await testSeondary.RunAsync();
                }
                else if (!string.IsNullOrWhiteSpace(X509CACertPath) && !string.IsNullOrWhiteSpace(X509CAKeyPath))
                {
                    // use X.509 CA auth and perform test using CA chained certificates
                    var testCa = new LeafDevice(
                        connectionString,
                        endpoint,
                        this.DeviceId,
                        this.TrustedCACertificateFileName,
                        this.EdgeHostName,
                        this.UseWebSockets,
                        X509CACertPath,
                        X509CAKeyPath);
                    await testCa.RunAsync();
                }
                else
                {
                    // non certificate flow use SAS tokens
                    var testSas = new LeafDevice(
                        connectionString,
                        endpoint,
                        this.DeviceId,
                        this.TrustedCACertificateFileName,
                        this.EdgeHostName,
                        this.UseWebSockets);
                    await testSas.RunAsync();
                }
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
