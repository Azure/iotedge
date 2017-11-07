// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    public class Program
    {
        const string ConfigFileName = "appsettings_agent.json";
        const string EdgeAgentStorageFolder = "edgeAgent";

        public static int Main()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            return MainAsync(configuration).Result;
        }

        public static async Task<int> MainAsync(IConfiguration configuration)
        {
            string dockerUriConfig = configuration.GetValue<string>("DockerUri");
            string configSourceConfig = configuration.GetValue<string>("ConfigSource");
            string dockerLoggingDriver = configuration.GetValue<string>("DockerLoggingDriver");
            Dictionary<string, string> dockerLoggingOptions = configuration.GetSection("DockerLoggingOptions").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            string backupConfigFilePath = configuration.GetValue<string>("BackupConfigFilePath");
            int maxRestartCount = configuration.GetValue<int>("MaxRestartCount");
            TimeSpan intensiveCareTime = TimeSpan.FromMinutes(configuration.GetValue<int>("IntensiveCareTimeInMinutes"));
            int coolOffTimeUnitInSeconds = configuration.GetValue<int>("CoolOffTimeUnitInSeconds");

            string logLevel = configuration.GetValue($"{Logger.RuntimeLogLevelEnvKey}", "info");
            Logger.SetLogLevel(logLevel);

            bool usePersistentStorage = configuration.GetValue<bool>("UsePersistentStorage", true);
            string storagePath = usePersistentStorage ? GetStoragePath(configuration) : string.Empty;

            // build the logger instance for the Program type
            var loggerBuilder = new ContainerBuilder();
            loggerBuilder.RegisterModule(new LoggingModule(Preconditions.CheckNonWhiteSpace(dockerLoggingDriver, nameof(dockerLoggingDriver)),
                Preconditions.CheckNotNull(dockerLoggingOptions, nameof(dockerLoggingOptions))));
            var loggerFactory = loggerBuilder.Build().Resolve<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger<Program>();

            string deviceConnectionString = configuration.GetValue<string>("DeviceConnectionString");
            string edgeDeviceHostName = configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
            IotHubConnectionStringBuilder connectionStringParser = Client.IotHubConnectionStringBuilder.Create(deviceConnectionString);
            var edgeHubConnectionDetails = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(connectionStringParser.HostName, connectionStringParser.DeviceId)
                .SetSharedAccessKey(connectionStringParser.SharedAccessKey)
                .SetGatewayHostName(edgeDeviceHostName)
                .Build();

            var dockerUri = new Uri(dockerUriConfig);
            var builder = new ContainerBuilder();

            switch (configSourceConfig.ToLower())
            {
                case "iothubconnected":
                    builder.RegisterModule(new IotHubConnectedModule(dockerUri, dockerLoggingDriver, dockerLoggingOptions, edgeHubConnectionDetails, deviceConnectionString, backupConfigFilePath, maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, configuration, usePersistentStorage, storagePath));
                    break;
                case "standalone":
                    builder.RegisterModule(new StandaloneModule(dockerUri, dockerLoggingDriver, dockerLoggingOptions, "config.json", maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, configuration, edgeHubConnectionDetails, deviceConnectionString, usePersistentStorage, storagePath));
                    break;
                default:
                    throw new Exception("ConfigSource not Supported.");
            }

            logger.LogInformation("Starting module management agent.");
            LogLogo(logger);
            var cts = new CancellationTokenSource();

            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts, logger);

            try
            {
                IContainer container = builder.Build();

                AssemblyLoadContext.Default.Unloading += OnUnload;
                Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts, logger); };

                using (IConfigSource configSource = await container.Resolve<Task<IConfigSource>>())
                {
                    Agent agent = await container.Resolve<Task<Agent>>();
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await agent.ReconcileAsync(cts.Token);
                        }
                        catch (Exception ex) when (!ex.IsFatal())
                        {
                            logger.LogWarning(AgentEventIds.Agent, ex, "Agent reconcile concluded with errors.");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                    }
                    AssemblyLoadContext.Default.Unloading -= OnUnload;
                    logger.LogInformation("Closing module management agent.");

                }
                return 0;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Main thread terminated");
                AssemblyLoadContext.Default.Unloading -= OnUnload;
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error starting Agent.");
                return 1;
            }
        }

        static string GetStoragePath(IConfiguration configuration)
        {
            string baseStoragePath = configuration.GetValue<string>("StorageFolder");
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }
            string storagePath = Path.Combine(baseStoragePath, EdgeAgentStorageFolder);
            Directory.CreateDirectory(storagePath);
            return storagePath;
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
