// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
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
            string certPath = Path.Combine(
                configuration.GetValue<string>(Constants.SslCertPathEnvName),
                configuration.GetValue<string>(Constants.SslCertEnvName));
            Hosting hosting = Hosting.Initialize(certPath);

            IContainer container = hosting.Container;

            ILogger logger = container.Resolve<ILoggerFactory>().CreateLogger("EdgeHub");
            logger.LogInformation("Starting Edge Hub");
            VersionInfo versionInfo = VersionInfo.Get(Constants.VersionInfoFileName);
            if (versionInfo != VersionInfo.Empty)
            {
                logger.LogInformation($"Version - {versionInfo}");
            }
            LogLogo(logger);

            var cts = new CancellationTokenSource();

            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts, logger);
            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => CancelProgram(cts, logger);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogInformation("Installing intermediate certificates.");
                string chainPath = Environment.GetEnvironmentVariable("EdgeModuleHubServerCAChainCertificateFile");
                CertificateHelper.InstallCerts(
                    StoreName.CertificateAuthority,
                    StoreLocation.CurrentUser,
                    CertificateHelper.ExtractCertsFromPem(chainPath));
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
