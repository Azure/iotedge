// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    public class Program
    {
        static readonly TimeSpan ShutdownWaitPeriod = TimeSpan.FromSeconds(20);
        public static int Main()
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)}] Edge Hub Main()");
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(Constants.ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            return MainAsync(configuration).Result;
        }

        public static async Task<int> MainAsync(IConfigurationRoot configuration)
        {
            string logLevel = configuration.GetValue($"{Logger.RuntimeLogLevelEnvKey}", "info");
            Logger.SetLogLevel(logLevel);

            // Set the LoggerFactory used by the Routing code.
            if (configuration.GetValue("EnableRoutingLogging", false))
            {
                Routing.Core.Routing.LoggerFactory = Logger.Factory;
            }

            X509Certificate2 cert;
            IEnumerable<X509Certificate2> chain;
            string edgeHubConnectionString = configuration.GetValue<string>(Constants.IotHubConnectionStringVariableName);
            // When connection string is not set it is edged mode
            if (string.IsNullOrEmpty(edgeHubConnectionString))
            {
                var workloadUri = new Uri(configuration.GetValue<string>(Constants.WorkloadUriVariableName));
                string edgeHubHostname = configuration.GetValue<string>(Constants.EdgeDeviceHostnameVariableName);
                string moduleId = configuration.GetValue<string>(Constants.ModuleIdVariableName);
                string generationId = configuration.GetValue<string>(Constants.ModuleGenerationIdVariableName);
                DateTime expiration = DateTime.UtcNow.AddDays(Constants.CertificateValidityDays);
                (cert, chain) = await CertificateHelper.GetServerCertificatesFromEdgelet(workloadUri, Constants.WorkloadApiVersion, moduleId, generationId, edgeHubHostname, expiration);
            }
            else
            {
                string edgeHubCertPath = configuration.GetValue<string>(Constants.EdgeHubServerCertificateFileKey);
                cert = new X509Certificate2(edgeHubCertPath);
                string edgeHubCaChainCertPath = configuration.GetValue<string>(Constants.EdgeHubServerCAChainCertificateFileKey);
                chain = CertificateHelper.GetServerCACertificatesFromFile(edgeHubCaChainCertPath);
            }

            // TODO: set certificate for Startup without the cache
            ServerCertificateCache.X509Certificate = cert;

            int port = configuration.GetValue("httpSettings:port", 443);
            Hosting hosting = Hosting.Initialize(port);

            IContainer container = hosting.Container;

            ILogger logger = container.Resolve<ILoggerFactory>().CreateLogger("EdgeHub");
            logger.LogInformation("Starting Edge Hub");
            VersionInfo versionInfo = VersionInfo.Get(Constants.VersionInfoFileName);
            if (versionInfo != VersionInfo.Empty)
            {
                logger.LogInformation($"Version - {versionInfo.ToString(true)}");
            }
            LogLogo(logger);
            
            if (chain != null)
            {
                logger.LogInformation("Installing intermediate certificates.");

                CertificateHelper.InstallCerts(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root,
                    StoreLocation.CurrentUser,
                    chain);
            }
            else
            {
                logger.LogWarning("Unable to find intermediate certificates.");
            }

            // EdgeHub and CloudConnectionProvider have a circular dependency. So need to Bind the EdgeHub to the CloudConnectionProvider.
            IEdgeHub edgeHub = await container.Resolve<Task<IEdgeHub>>();
            ICloudConnectionProvider cloudConnectionProvider = await container.Resolve<Task<ICloudConnectionProvider>>();
            cloudConnectionProvider.BindEdgeHub(edgeHub);

            // Register EdgeHub credentials
            var edgeHubCredentials = container.ResolveNamed<IClientCredentials>("EdgeHubCredentials");
            ICredentialsCache credentialsCache = await container.Resolve<Task<ICredentialsCache>>();
            await credentialsCache.Add(edgeHubCredentials);

            // EdgeHub cloud proxy and DeviceConnectivityManager have a circular dependency,
            // so the cloud proxy has to be set on the DeviceConnectivityManager after both have been initialized.
            var deviceConnectivityManager = container.Resolve<IDeviceConnectivityManager>();
            IConnectionManager connectionManager = await container.Resolve<Task<IConnectionManager>>();
            (deviceConnectivityManager as DeviceConnectivityManager)?.SetConnectionManager(connectionManager);

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

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler)
                = ShutdownHandler.Init(ShutdownWaitPeriod, logger);

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

            using (IProtocolHead protocolHead = new EdgeHubProtocolHead(protocolHeads, logger))
            {
                await protocolHead.StartAsync();
                await cts.Token.WhenCanceled();
                await Task.WhenAny(protocolHead.CloseAsync(CancellationToken.None), Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None));
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            return 0;
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
