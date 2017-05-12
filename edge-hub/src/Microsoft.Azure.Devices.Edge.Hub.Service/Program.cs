// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    class Program
    {
        const int DefaultConnectionPoolSize = 400; // IoT Hub default connection pool size
        const string configFileName = "appsettings.json";
        const string topicNameConversionSectionName = "mqttTopicNameConversion";
        const string sslCertPathEnvName = "SSL_CERTIFICATE_PATH";
        const string sslCertEnvName = "SSL_CERTIFICATE_NAME";
        const string connectioPoolSizeConfigName = "IotHubClient.ConnectionPoolSize";
        static readonly IConfigurationRoot configurationRoot = new ConfigurationBuilder()
            .AddJsonFile(configFileName)
            .AddEnvironmentVariables()
            .Build();

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            ILogger logger =  EdgeLogging.LoggerFactory.CreateLogger<Program>();

            logger.LogInformation("Starting local IoT Hub.");

            var eventListener = new ConsoleEventListner();

            eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Verbose);
            eventListener.EnableEvents(DefaultEventSource.Log, EventLevel.Verbose);

            var cts = new CancellationTokenSource();

            // TODO: Read certificate from device secret store
            string certPath = Path.Combine(configurationRoot.GetValue<string>(sslCertPathEnvName), configurationRoot.GetValue<string>(sslCertEnvName));
            var certificate = new X509Certificate2(certPath);
            var settingsProvider = new AppConfigSettingsProvider();
         
            IMessageConverter<Client.Message> deviceClientMessageConverter = new MqttMessageConverter();
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(logger, deviceClientMessageConverter);

            IConnectionManager connectionManager = new ConnectionManager(cloudProxyProvider);
            IDispatcher dispatcher = new Dispatcher(connectionManager);
            IRouter router = new Router(dispatcher);

            var configuration = new MessageAddressConversionConfiguration();
            configurationRoot.GetSection(topicNameConversionSectionName).Bind(configuration);
            var messageAddressConverter = new MessageAddressConverter(configuration);
            IMessageConverter<IProtocolGatewayMessage> pgMessageConverter = new ProtocolGatewayMessageConverter(messageAddressConverter);
            
            IConnectionProvider connectionProvider = new ConnectionProvider(connectionManager, router, dispatcher);
            IMqttConnectionProvider mqttConnectionProvider = new MqttConnectionProvider(connectionProvider, pgMessageConverter);

            var bootstrapper = new MqttBootstrapper(settingsProvider, certificate, mqttConnectionProvider, connectionManager);

            await bootstrapper.StartAsync(cts.Token);

            while (true)
            {
                string input = Console.ReadLine();
                if (input != null && input.ToLowerInvariant() == "exit")
                {
                    break;
                }
            }

            cts.Cancel();
            bootstrapper.CloseCompletion.Wait(TimeSpan.FromSeconds(20));

            return 0;
        }        
    }
}