// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.IO;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    public class Program
    {
        const string SslCertPathEnvName = "SSL_CERTIFICATE_PATH";
        const string SslCertEnvName = "SSL_CERTIFICATE_NAME";

        public static int Main()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            return MainAsync(configuration).Result;
        }

        public static async Task<int> MainAsync(IConfigurationRoot configuration)
        {
            string logLevel = configuration.GetValue($"{Logger.RuntimeLogLevelEnvKey}", "info");
            Logger.SetLogLevel(logLevel);

            // Set the LoggerFactory used by the Routing code. 
            Routing.Core.Routing.LoggerFactory = Logger.Factory;

            string certPath = Path.Combine(configuration.GetValue<string>(SslCertPathEnvName), configuration.GetValue<string>(SslCertEnvName));
            var certificate = new X509Certificate2(certPath);
            var hosting = new Hosting();
            hosting.Initialize(certificate);

            IContainer container = hosting.Container;

            ILogger logger = container.Resolve<ILoggerFactory>().CreateLogger("EdgeHub");
            logger.LogInformation("Starting Edge Hub.");
            LogLogo(logger);
            var cts = new CancellationTokenSource();

            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts, logger);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => CancelProgram(cts, logger);

            logger.LogInformation("Initializing configuration");
            IConfigSource configSource = await container.Resolve<Task<IConfigSource>>();
            ConfigUpdater configUpdater = await container.Resolve<Task<ConfigUpdater>>();
            await configUpdater.Init(configSource);

            logger.LogInformation("Starting Http Server");
            hosting.Start();

            logger.LogInformation("Starting MQTT Server");
            using (IMqttConnectionProvider connectionProvider = await container.Resolve<Task<IMqttConnectionProvider>>())
            using (IProtocolHead protocolHead = new MqttProtocolHead(container.Resolve<ISettingsProvider>(), certificate, connectionProvider, container.Resolve<IDeviceIdentityProvider>(), container.Resolve<ISessionStatePersistenceProvider>()))
            {
                await protocolHead.StartAsync();

                await cts.Token.WhenCanceled();

                logger.LogInformation("Closing protocol Head.");

                await Task.WhenAny(protocolHead.CloseAsync(CancellationToken.None), Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None));

                AssemblyLoadContext.Default.Unloading -= OnUnload;
            }

            return 0;
        }

        static void CancelProgram(CancellationTokenSource cts, ILogger logger)
        {
            logger.LogInformation("Termination requested, closing.");
            cts.Cancel();
        }

        static void LogLogo(ILogger logger)
        {
            logger.LogInformation(@"
        █████╗ ███████╗██╗   ██╗██████╗ ███████╗
       ██╔══██╗╚══███╔╝██║   ██║██╔══██╗██╔════╝
       ███████║  ███╔╝ ██║   ██║██████╔╝█████╗
       ██╔══██║ ███╔╝  ██║   ██║██╔══██╗██╔══╝
       ██║  ██║███████╗╚██████╔╝██║  ██║███████╗
       ╚═╝  ╚═╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝

██╗ ██████╗ ████████╗    ███████╗██████╗  ██████╗ ███████╗
██║██╔═══██╗╚══██╔══╝    ██╔════╝██╔══██╗██╔════╝ ██╔════╝
██║██║   ██║   ██║       █████╗  ██║  ██║██║  ███╗█████╗
██║██║   ██║   ██║       ██╔══╝  ██║  ██║██║   ██║██╔══╝
██║╚██████╔╝   ██║       ███████╗██████╔╝╚██████╔╝███████╗
╚═╝ ╚═════╝    ╚═╝       ╚══════╝╚═════╝  ╚═════╝ ╚══════╝
");
        }
    }
}
