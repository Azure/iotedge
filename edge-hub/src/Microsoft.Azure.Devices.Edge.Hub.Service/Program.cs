// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    class Program
    {
        const string ConfigFileName = "appsettings.json";
        const string TopicNameConversionSectionName = "mqttTopicNameConversion";
        const string SslCertPathEnvName = "SSL_CERTIFICATE_PATH";
        const string SslCertEnvName = "SSL_CERTIFICATE_NAME";

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            string certPath = Path.Combine(configuration.GetValue<string>(SslCertPathEnvName), configuration.GetValue<string>(SslCertEnvName));
            var certificate = new X509Certificate2(certPath);

            string iothubHostname = configuration.GetValue<string>("IotHubHostName");
            string edgeDeviceId = configuration.GetValue<string>("EdgeDeviceId");
            var topics = new MessageAddressConversionConfiguration(
                configuration.GetSection(TopicNameConversionSectionName + ":InboundTemplates").Get<List<string>>(),
                configuration.GetSection(TopicNameConversionSectionName + ":OutboundTemplates").Get<List<string>>());
            var routes = configuration.GetSection("routes").Get<List<string>>();

            var builder = new ContainerBuilder();
            builder.RegisterModule(new LoggingModule());
            builder.RegisterModule(new MqttModule(certificate, topics, iothubHostname, edgeDeviceId));
            builder.RegisterModule(new RoutingModule(iothubHostname, edgeDeviceId, routes));
            IContainer container = builder.Build();

            ILogger logger = container.Resolve<ILoggerFactory>().CreateLogger("EdgeHub");
            logger.LogInformation("Starting Edge Hub.");

            using (IProtocolHead protocolHead = await container.Resolve<Task<IProtocolHead>>())
            {
                await protocolHead.StartAsync();

                while (true)
                {
                    string input = Console.ReadLine();
                    if (input != null && input.ToLowerInvariant() == "exit")
                    {
                        break;
                    }
                }

                await Task.WhenAny(protocolHead.CloseAsync(CancellationToken.None), Task.Delay(TimeSpan.FromSeconds(20)));
            }

            return 0;
        }
    }
}