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
                (cert, chain) = CertificateHelper.GetServerCertificatesFromFile(configuration.GetValue<string>(Constants.SslCertPathEnvName), configuration.GetValue<string>(Constants.SslCertEnvName));
            }

            // TODO: set certificate for Startup without the cache
            ServerCertificateCache.X509Certificate = cert;
            Hosting hosting = Hosting.Initialize();

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

            // EdgeHub cloud proxy and DeviceConnectivityManager have a circular dependency,
            // so the cloud proxy has to be set on the DeviceConnectivityManager after both have been initialized.
            var deviceConnectivityManager = container.Resolve<IDeviceConnectivityManager>();
            ICloudProxy cloudProxy = await container.ResolveNamed<Task<ICloudProxy>>("EdgeHubCloudProxy");
            (deviceConnectivityManager as DeviceConnectivityManager)?.SetTestCloudProxy(cloudProxy);

            logger.LogInformation("Initializing configuration");
            IConfigSource configSource = await container.Resolve<Task<IConfigSource>>();
            ConfigUpdater configUpdater = await container.Resolve<Task<ConfigUpdater>>();
            await configUpdater.Init(configSource);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler)
                = ShutdownHandler.Init(ShutdownWaitPeriod, logger);

            using (IProtocolHead protocolHead = new EdgeHubProtocolHead(
                new IProtocolHead[]
                {
                    new HttpProtocolHead(hosting.WebHost),
                    await container.Resolve<Task<MqttProtocolHead>>(),
                    await container.Resolve<Task<AmqpProtocolHead>>()
                }, logger))
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
