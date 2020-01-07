// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class Program
    {
        const int DefaultShutdownWaitPeriod = 60;
        const SslProtocols DefaultSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        public static int Main()
        {
            Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Edge Hub Main()");
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(Constants.ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            return MainAsync(configuration).Result;
        }

        static async Task<int> MainAsync(IConfigurationRoot configuration)
        {
            string logLevel = configuration.GetValue($"{Logger.RuntimeLogLevelEnvKey}", "info");
            Logger.SetLogLevel(logLevel);

            // Set the LoggerFactory used by the Routing code.
            if (configuration.GetValue("EnableRoutingLogging", false))
            {
                Routing.LoggerFactory = Logger.Factory;
            }

            ILogger logger = Logger.Factory.CreateLogger("EdgeHub");

            EdgeHubCertificates certificates = await EdgeHubCertificates.LoadAsync(configuration, logger);
            bool clientCertAuthEnabled = configuration.GetValue(Constants.ConfigKey.EdgeHubClientCertAuthEnabled, false);

            string sslProtocolsConfig = configuration.GetValue(Constants.ConfigKey.SslProtocols, string.Empty);
            SslProtocols sslProtocols = SslProtocolsHelper.Parse(sslProtocolsConfig, DefaultSslProtocols, logger);
            logger.LogInformation($"Enabling SSL protocols: {sslProtocols.Print()}");

            IDependencyManager dependencyManager = new DependencyManager(configuration, certificates.ServerCertificate, certificates.TrustBundle, sslProtocols);
            Hosting hosting = Hosting.Initialize(configuration, certificates.ServerCertificate, dependencyManager, clientCertAuthEnabled, sslProtocols);
            IContainer container = hosting.Container;

            logger.LogInformation("Initializing Edge Hub");
            LogLogo(logger);
            LogVersionInfo(logger);
            logger.LogInformation($"OptimizeForPerformance={configuration.GetValue("OptimizeForPerformance", true)}");
            logger.LogInformation("Loaded server certificate with expiration date of {0}", certificates.ServerCertificate.NotAfter.ToString("o"));

            var metricsListener = container.Resolve<IMetricsListener>();
            var metricsProvider = container.Resolve<IMetricsProvider>();
            Metrics.Init(metricsProvider, metricsListener, logger);

            // Init V0 Metrics
            MetricsV0.BuildMetricsCollector(configuration);

            // EdgeHub and CloudConnectionProvider have a circular dependency. So need to Bind the EdgeHub to the CloudConnectionProvider.
            IEdgeHub edgeHub = await container.Resolve<Task<IEdgeHub>>();
            ICloudConnectionProvider cloudConnectionProvider = await container.Resolve<Task<ICloudConnectionProvider>>();
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // EdgeHub cloud proxy and DeviceConnectivityManager have a circular dependency,
            // so the cloud proxy has to be set on the DeviceConnectivityManager after both have been initialized.
            var deviceConnectivityManager = container.Resolve<IDeviceConnectivityManager>();
            IConnectionManager connectionManager = await container.Resolve<Task<IConnectionManager>>();
            (deviceConnectivityManager as DeviceConnectivityManager)?.SetConnectionManager(connectionManager);

            // Register EdgeHub credentials
            var edgeHubCredentials = container.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
            ICredentialsCache credentialsCache = await container.Resolve<Task<ICredentialsCache>>();
            await credentialsCache.Add(edgeHubCredentials);

            // Initializing configuration
            logger.LogInformation("Initializing configuration");
            IConfigSource configSource = await container.Resolve<Task<IConfigSource>>();
            ConfigUpdater configUpdater = await container.Resolve<Task<ConfigUpdater>>();
            await configUpdater.Init(configSource);

            if (!Enum.TryParse(configuration.GetValue("AuthenticationMode", string.Empty), true, out AuthenticationMode authenticationMode)
                || authenticationMode != AuthenticationMode.Cloud)
            {
                ConnectionReauthenticator connectionReauthenticator = await container.Resolve<Task<ConnectionReauthenticator>>();
                connectionReauthenticator.Init();
            }

            TimeSpan shutdownWaitPeriod = TimeSpan.FromSeconds(configuration.GetValue("ShutdownWaitPeriod", DefaultShutdownWaitPeriod));
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(shutdownWaitPeriod, logger);

            using (IProtocolHead protocolHead = await GetEdgeHubProtocolHeadAsync(logger, configuration, container, hosting))
            using (var renewal = new CertificateRenewal(certificates, logger))
            {
                await protocolHead.StartAsync();
                await Task.WhenAny(cts.Token.WhenCanceled(), renewal.Token.WhenCanceled());
                logger.LogInformation("Stopping the protocol heads...");
                await protocolHead.CloseAsync(CancellationToken.None);
                logger.LogInformation("Protocol heads stopped.");

                await CloseDbStoreProviderAsync(container);
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            logger.LogInformation("Shutdown complete.");
            return 0;
        }

        static void LogVersionInfo(ILogger logger)
        {
            VersionInfo versionInfo = VersionInfo.Get(Constants.VersionInfoFileName);
            if (versionInfo != VersionInfo.Empty)
            {
                logger.LogInformation($"Version - {versionInfo.ToString(true)}");
            }
        }

        static async Task<EdgeHubProtocolHead> GetEdgeHubProtocolHeadAsync(ILogger logger, IConfigurationRoot configuration, IContainer container, Hosting hosting)
        {
            var protocolHeads = new List<IProtocolHead>();
            if (configuration.GetValue("mqttSettings:enabled", true))
            {
                protocolHeads.Add(await container.Resolve<Task<MqttProtocolHead>>());
            }

            if (configuration.GetValue("amqpSettings:enabled", true))
            {
                protocolHeads.Add(await container.Resolve<Task<AmqpProtocolHead>>());
            }

            if (configuration.GetValue("httpSettings:enabled", true))
            {
                protocolHeads.Add(new HttpProtocolHead(hosting.WebHost));
            }

            return new EdgeHubProtocolHead(protocolHeads, logger);
        }

        static async Task CloseDbStoreProviderAsync(IContainer container)
        {
            IDbStoreProvider dbStoreProvider = await container.Resolve<Task<IDbStoreProvider>>();
            await dbStoreProvider.CloseAsync();
        }

        static void LogLogo(ILogger logger)
        {
            logger.LogInformation(
                @"
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
