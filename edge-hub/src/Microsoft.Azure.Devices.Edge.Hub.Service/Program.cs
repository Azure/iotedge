// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
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
                configuration.GetSection(TopicNameConversionSectionName + ":OutboundTemplates").Get<Dictionary<string,string>>());
            var routes = configuration.GetSection("routes").Get<List<string>>();

            var builder = new ContainerBuilder();
            builder.RegisterModule(new LoggingModule());

            builder.RegisterBuildCallback(
                c =>
                {
                    // set up loggers for dotnetty
                    var loggerFactory = c.Resolve<ILoggerFactory>();
                    InternalLoggerFactory.DefaultFactory = loggerFactory;

                    var eventListener = new LoggerEventListener(loggerFactory.CreateLogger("ProtocolGateway"));
                    eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Informational);
                });

            builder.RegisterModule(new MqttModule(certificate, topics, iothubHostname, edgeDeviceId));
            builder.RegisterModule(new RoutingModule(iothubHostname, edgeDeviceId, routes));
            IContainer container = builder.Build();

            ILogger logger = container.Resolve<ILoggerFactory>().CreateLogger("EdgeHub");
            logger.LogInformation("Starting Edge Hub.");
            var cts = new CancellationTokenSource();

            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts, logger);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts, logger); };

            using (IProtocolHead protocolHead = await container.Resolve<Task<IProtocolHead>>())
            {
                await protocolHead.StartAsync();

                await cts.Token.WhenCanceled();

                logger.LogInformation("Closing protocol Head.");

                await Task.WhenAny(protocolHead.CloseAsync(CancellationToken.None), Task.Delay(TimeSpan.FromSeconds(10)));

                AssemblyLoadContext.Default.Unloading -= OnUnload;
            }

            return 0;
        }

        static void CancelProgram(CancellationTokenSource cts, ILogger logger)
        {
            logger.LogInformation("Termination requested, closing.");
            cts.Cancel();
        }
    }
}