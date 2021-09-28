// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter;
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

            IDependencyManager dependencyManager = new DependencyManager(configuration, certificates.ServerCertificate, certificates.TrustBundle, certificates.ManifestTrustBundle, sslProtocols);
            Hosting hosting = Hosting.Initialize(configuration, certificates.ServerCertificate, dependencyManager, clientCertAuthEnabled, sslProtocols);
            IContainer container = hosting.Container;

            logger.LogInformation("Initializing Edge Hub");
            LogLogo(logger);
            LogVersionInfo(logger);
            logger.LogInformation($"OptimizeForPerformance={configuration.GetValue("OptimizeForPerformance", true)}");
            logger.LogInformation($"MessageAckTimeoutSecs={configuration.GetValue("MessageAckTimeoutSecs", 30)}");
            logger.LogInformation("Loaded server certificate with expiration date of {0}", certificates.ServerCertificate.NotAfter.ToString("o"));

            var metricsProvider = container.Resolve<IMetricsProvider>();
            Metrics.InitWithAspNet(metricsProvider, logger); // Note this requires App.UseMetricServer() to be called in Startup.cs

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

            // Register EdgeHub indentity in device scopes cache.
            // When we connect upstream, we verify that identity is in scope.
            // On a fresh start, we may not yet received the scopes from the upstream, so we need
            // to force add edgeHub in the cache so it is able to connect upstream.
            // Once we get the scopes from the upstream, this record is replaced.
            ServiceIdentity edgeHubIdentity = container.ResolveNamed<ServiceIdentity>("EdgeHubIdentity");
            IServiceIdentityHierarchy identityScopes = container.Resolve<IServiceIdentityHierarchy>();
            await identityScopes.AddOrUpdate(edgeHubIdentity);

            // Initializing configuration
            logger.LogInformation("Initializing configuration");
            IConfigSource configSource = await container.Resolve<Task<IConfigSource>>();
            ConfigUpdater configUpdater = await container.Resolve<Task<ConfigUpdater>>();
            ExperimentalFeatures experimentalFeatures = CreateExperimentalFeatures(configuration);
            var configDownloadTask = configUpdater.Init(configSource);

            if (!Enum.TryParse(configuration.GetValue("AuthenticationMode", string.Empty), true, out AuthenticationMode authenticationMode)
                || authenticationMode != AuthenticationMode.Cloud)
            {
                ConnectionReauthenticator connectionReauthenticator = await container.Resolve<Task<ConnectionReauthenticator>>();
                connectionReauthenticator.Init();
            }

            TimeSpan shutdownWaitPeriod = TimeSpan.FromSeconds(configuration.GetValue("ShutdownWaitPeriod", DefaultShutdownWaitPeriod));
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(shutdownWaitPeriod, logger);

            using (IProtocolHead mqttBrokerProtocolHead = await GetMqttBrokerProtocolHeadAsync(experimentalFeatures, container))
            using (IProtocolHead edgeHubProtocolHead = await GetEdgeHubProtocolHeadAsync(logger, configuration, experimentalFeatures, container, hosting))
            using (var renewal = new CertificateRenewal(certificates, logger))
            {
                try
                {
                    await configDownloadTask;
                    await Task.WhenAll(mqttBrokerProtocolHead.StartAsync(), edgeHubProtocolHead.StartAsync());
                    await Task.WhenAny(cts.Token.WhenCanceled(), renewal.Token.WhenCanceled());
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error starting protocol heads: {ex.Message}");
                }

                logger.LogInformation("Stopping the protocol heads...");
                await Task.WhenAll(mqttBrokerProtocolHead.CloseAsync(CancellationToken.None), edgeHubProtocolHead.CloseAsync(CancellationToken.None));
                logger.LogInformation("Protocol heads stopped.");

                await CloseDbStoreProviderAsync(container);
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            logger.LogInformation("Shutdown complete.");
            return 0;
        }

        static ExperimentalFeatures CreateExperimentalFeatures(IConfigurationRoot configuration)
        {
            IConfiguration experimentalFeaturesConfig = configuration.GetSection(Constants.ConfigKey.ExperimentalFeatures);
            ExperimentalFeatures experimentalFeatures = ExperimentalFeatures.Create(experimentalFeaturesConfig, Logger.Factory.CreateLogger("EdgeHub"));
            return experimentalFeatures;
        }

        static void LogVersionInfo(ILogger logger)
        {
            VersionInfo versionInfo = VersionInfo.Get(Constants.VersionInfoFileName);
            if (versionInfo != VersionInfo.Empty)
            {
                logger.LogInformation($"Version - {versionInfo.ToString(true)}");
            }
        }

        static async Task<IProtocolHead> GetMqttBrokerProtocolHeadAsync(ExperimentalFeatures experimentalFeatures, IContainer container)
        {
            if (!experimentalFeatures.EnableMqttBroker)
            {
                return EmptyProtocolHead.GetInstance();
            }

            var orderedProtocolHeads = new List<IProtocolHead>();
            orderedProtocolHeads.Add(container.Resolve<MqttBrokerProtocolHead>());
            orderedProtocolHeads.Add(await container.Resolve<Task<AuthAgentProtocolHead>>());
            return new OrderedProtocolHead(orderedProtocolHeads);
        }

        static async Task<IProtocolHead> GetEdgeHubProtocolHeadAsync(ILogger logger, IConfigurationRoot configuration, ExperimentalFeatures experimentalFeatures, IContainer container, Hosting hosting)
        {
            var protocolHeads = new List<IProtocolHead>();

            // MQTT broker overrides the legacy MQTT protocol head
            if (configuration.GetValue("mqttSettings:enabled", true) && !experimentalFeatures.EnableMqttBroker)
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
