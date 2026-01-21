// Copyright (c) Microsoft. All rights reserved.
namespace LeafDeviceTest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    [Command(
        Name = "LeafDevice",
        Description = "An app which installs a certificate into cert store, creates a device and sends message and check if the message was received. This is to be used on a transparent Gateway setup for testing.",
        ExtendedHelpText = @"
Environment Variables:
  Most options in this command override environment variables. In other words,
  the value of the corresponding environment variable will be used unless the
  option is specified on the command line.

  Option                        Environment variable
  --event-hub-name              eventHubName
  --fully-qualified-namespace   fullyQualifiedNamespace
  --iothub-hostname             iothubHostName
  --proxy                       https_proxy

Defaults:
  All options to this command have defaults. If an option is not specified and
  its corresponding environment variable is not defined, then the default will
  be used.

  Option                        Default value
  --event-hub-name              get the value from Key Vault
  --fully-qualified-namespace   get the value from Key Vault
  --iothub-hostname             get the value from Key Vault
  --device-id                   an auto-generated unique identifier
  --certificate                 empty string
  --edge-hostname               empty string
  --proxy                       no proxy is used
")]
    [HelpOption]
    class Program
    {
        [Option("--event-hub-name <value>", Description = "Event Hub name")]
        public string EventHubName { get; } = Environment.GetEnvironmentVariable("eventHubName");

        [Option("--fully-qualified-namespace <value>", Description = "Fully qualified namespace for Event Hub")]
        public string FullyQualifiedNamespace { get; } = Environment.GetEnvironmentVariable("fullyQualifiedNamespace");

        [Option("-ct|--certificate <value>", Description = "Trust bundle CA Certificate(s) file to be installed on the machine.")]
        public string TrustedCACertificateFileName { get; } = string.Empty;

        [Option("-d|--device-id", Description = "Leaf device identifier to be registered with IoT Hub")]
        public string DeviceId { get; } = $"leaf-device--{Guid.NewGuid()}";

        [Option("-ed|--edge-hostname", Description = "Hostname of the Edge device that acts as a gateway to the leaf device")]
        public string EdgeHostName { get; } = string.Empty;

        [Option("-ed-id|--edge-device-id", Description = @"Device Id of the Edge device that acts as a gateway to the leaf device.
                                                         If not provided, the leaf device will not be in the Edge device's scope")]
        public string EdgeGatewayDeviceId { get; } = string.Empty;

        [Option("|--iothub-hostname <value>", Description = "IoT hub hostname")]
        public string IotHubHostName { get; } = Environment.GetEnvironmentVariable("iothubHostName");

        [Option("-proto|--protocol", Description = @"Protocol the leaf device will use to communicate with the Edge device.
                                                    Choices are Mqtt, MqttWs, Amqp, AmqpWs.
                                                    If protocol is unspecified, default is Mqtt.")]
        public DeviceProtocol Protocol { get; } = DeviceProtocol.Mqtt;

        [Option("--proxy <value>", CommandOptionType.SingleValue, Description = "Proxy for IoT Hub connections.")]
        public (bool useProxy, string proxyUrl) Proxy { get; } = (false, string.Empty);

        [Option("-cac|--x509-ca-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format to be used for X.509 CA authentication.")]
        public string X509CACertPath { get; } = string.Empty;

        [Option("-cak|--x509-ca-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format to be used for X.509 CA authentication.")]
        public string X509CAKeyPath { get; } = string.Empty;

        [Option("-ctpc|--x509-primary-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format. This is needed for thumbprint auth and used as the primary certificate.")]
        public string X509PrimaryCertPath { get; } = string.Empty;

        [Option("-ctpk|--x509-primary-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format. This is needed for thumbprint auth and used as the primary certificate's key.")]
        public string X509PrimaryKeyPath { get; } = string.Empty;

        [Option("-ctsc|--x509-secondary-cert-path", Description = "Path to a X.509 leaf certificate file in PEM format. This is needed for thumbprint auth and used as the secondary certificate.")]
        public string X509SecondaryCertPath { get; } = string.Empty;

        [Option("-ctsk|--x509-secondary-key-path", Description = "Path to a X.509 leaf certificate key file in PEM format. This is needed for thumbprint auth and used as the secondary certificate's key.")]
        public string X509SecondaryKeyPath { get; } = string.Empty;

        [Option(
            "--use-secondary-credential",
            Description = "Set value to true if the secondary credential (either certificate or SharedAccessKey) should be used for authentication, " +
                          "otherwise the primary credential is used by default. Note: currently this is applicable for certificates tests only.")]
        public bool UseSecondaryCredential { get; } = false;

        // ReSharper disable once UnusedMember.Local
        static int Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args).Result;

        // ReSharper disable once UnusedMember.Local
        async Task<int> OnExecuteAsync()
        {
            try
            {
                string eventHubName = this.EventHubName ??
                                  await SecretsHelper.GetSecretFromConfigKey("eventHubName");

                string fullyQualifiedNamespace = this.FullyQualifiedNamespace ??
                                  await SecretsHelper.GetSecretFromConfigKey("fullyQualifiedNamespace");

                string iothubHostName = this.IotHubHostName ??
                                          await SecretsHelper.GetSecretFromConfigKey("iotHubHostName");

                (bool useProxy, string proxyUrl) = this.Proxy;
                Option<string> proxy = useProxy
                    ? Option.Some(proxyUrl)
                    : Option.Maybe(Environment.GetEnvironmentVariable("https_proxy"));

                var builder = new LeafDevice.LeafDeviceBuilder(
                    eventHubName,
                    fullyQualifiedNamespace,
                    iothubHostName,
                    this.DeviceId,
                    this.TrustedCACertificateFileName,
                    this.EdgeHostName,
                    this.EdgeGatewayDeviceId,
                    this.Protocol,
                    proxy);

                if (!string.IsNullOrWhiteSpace(this.X509PrimaryCertPath) &&
                    !string.IsNullOrWhiteSpace(this.X509PrimaryKeyPath) &&
                    !string.IsNullOrWhiteSpace(this.X509SecondaryCertPath) &&
                    !string.IsNullOrWhiteSpace(this.X509SecondaryKeyPath))
                {
                    // use thumbprint auth and perform test for both primary and secondary certificates
                    builder.SetX509ThumbprintAuthProperties(
                        this.X509PrimaryCertPath,
                        this.X509PrimaryKeyPath,
                        this.X509SecondaryCertPath,
                        this.X509SecondaryKeyPath,
                        !this.UseSecondaryCredential);
                    LeafDevice testThumbprintCertificate = builder.Build();
                    await testThumbprintCertificate.RunAsync();
                }
                else if (!string.IsNullOrWhiteSpace(this.X509CACertPath) &&
                         !string.IsNullOrWhiteSpace(this.X509CAKeyPath))
                {
                    // use X.509 CA auth and perform test using CA chained certificates
                    builder.SetX509CAAuthProperties(
                        this.X509CACertPath,
                        this.X509CAKeyPath);
                    LeafDevice testCa = builder.Build();
                    await testCa.RunAsync();
                }
                else
                {
                    // non certificate flow use SAS tokens
                    LeafDevice testSas = builder.Build();
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
