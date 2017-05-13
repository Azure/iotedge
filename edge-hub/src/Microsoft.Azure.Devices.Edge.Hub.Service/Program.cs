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
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;
    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Client.Message;
    using SimpleRouteFactory = Microsoft.Azure.Devices.Edge.Hub.Core.Routing.SimpleRouteFactory;

    class Program
    {
        static readonly RetryStrategy DefaultRetryStrategy = new FixedInterval(0, TimeSpan.FromSeconds(1));
        static readonly TimeSpan DefaultRevivePeriod = TimeSpan.FromHours(1);
        static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        static readonly EndpointExecutorConfig DefaultEndpointExecutorConfig = new EndpointExecutorConfig(DefaultTimeout, DefaultRetryStrategy, DefaultRevivePeriod, true);
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
         
            string iotHubName = settingsProvider.GetSetting("IotHubHostName");
            Core.IMessageConverter<Message> deviceClientMessageConverter = new MqttMessageConverter();
            ICloudProxyProvider cloudProxyProvider = new CloudProxyProvider(logger, deviceClientMessageConverter);
            IConnectionManager connectionManager = new ConnectionManager(cloudProxyProvider);
            Core.IMessageConverter<IRoutingMessage> routingMessageConverter = new RoutingMessageConverter();
            Router router = await SetupRouter(connectionManager, routingMessageConverter, iotHubName);
            IEdgeHub edgeHub = new RoutingEdgeHub(router, routingMessageConverter);
            var configuration = new MessageAddressConversionConfiguration();
            configurationRoot.GetSection(topicNameConversionSectionName).Bind(configuration);
            var messageAddressConverter = new MessageAddressConverter(configuration);
            Core.IMessageConverter<IProtocolGatewayMessage> pgMessageConverter = new ProtocolGatewayMessageConverter(messageAddressConverter);
           
            IConnectionProvider connectionProvider = new ConnectionProvider(connectionManager, edgeHub);
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

        static Task<Router> SetupRouter(IConnectionManager connectionManager, Core.IMessageConverter<IRoutingMessage> routingMessageConverter, string iotHubName)
        {
            Routing.PerfCounter = new NullRoutingPerfCounter();
            Routing.UserAnalyticsLogger = new NullUserAnalyticsLogger();
            Routing.UserMetricLogger = new NullRoutingUserMetricLogger();
                        
            IEndpointFactory endpointFactory = new SimpleEndpointFactory(connectionManager, routingMessageConverter);
            IRouteFactory routerFactory = new SimpleRouteFactory(endpointFactory);

            // TODO - For now, SimpleRouterFactory always returns a Cloud route (for proxy)
            Route route = routerFactory.Create(string.Empty);
            var config = new RouterConfig(route.Endpoints, new [] { route });
            return Router.CreateAsync(Guid.NewGuid().ToString(), iotHubName, config, new SyncEndpointExecutorFactory(DefaultEndpointExecutorConfig));            
        }
    }
}