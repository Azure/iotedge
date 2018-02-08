// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
        const string VersionInfoFileName = "versionInfo.json";

        public static int Main()
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile(ConfigFileName)
                    .AddEnvironmentVariables()
                    .Build();

                return MainAsync(configuration).Result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        public static async Task<int> MainAsync(IConfiguration configuration)
        {
            string dockerLoggingDriver = configuration.GetValue<string>("DockerLoggingDriver");
            Dictionary<string, string> dockerLoggingOptions = configuration.GetSection("DockerLoggingOptions").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

            // Bring up the logger before anything else so we can log errors ASAP
            ILogger logger = SetupLogger(configuration, dockerLoggingDriver, dockerLoggingOptions);

            logger.LogInformation("Starting module management agent.");

            VersionInfo versionInfo = VersionInfo.Get(VersionInfoFileName);
            if (versionInfo != VersionInfo.Empty)
            {
                logger.LogInformation($"Version - {versionInfo}");
            }
            LogLogo(logger);

            Uri dockerUri;
            string configSourceConfig;
            string backupConfigFilePath;
            int maxRestartCount;
            TimeSpan intensiveCareTime;
            int coolOffTimeUnitInSeconds;
            bool usePersistentStorage;
            string storagePath;
            string deviceConnectionString;
            string edgeDeviceHostName;

            try
            {
                dockerUri = new Uri(configuration.GetValue<string>("DockerUri"));
                configSourceConfig = configuration.GetValue<string>("ConfigSource");
                backupConfigFilePath = configuration.GetValue<string>("BackupConfigFilePath");
                maxRestartCount = configuration.GetValue<int>("MaxRestartCount");
                intensiveCareTime = TimeSpan.FromMinutes(configuration.GetValue<int>("IntensiveCareTimeInMinutes"));
                coolOffTimeUnitInSeconds = configuration.GetValue<int>("CoolOffTimeUnitInSeconds");
                usePersistentStorage = configuration.GetValue("UsePersistentStorage", true);
                storagePath = usePersistentStorage ? GetStoragePath(configuration) : string.Empty;
                deviceConnectionString = configuration.GetValue<string>("DeviceConnectionString");
                edgeDeviceHostName = configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error reading the Agent's configuration.");
                return 1;
            }

            EdgeHubConnectionString edgeHubConnectionDetails;

            try
            {
                IotHubConnectionStringBuilder connectionStringParser = IotHubConnectionStringBuilder.Create(deviceConnectionString);
                edgeHubConnectionDetails = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(connectionStringParser.HostName, connectionStringParser.DeviceId)
                    .SetSharedAccessKey(connectionStringParser.SharedAccessKey)
                    .SetGatewayHostName(edgeDeviceHostName)
                    .Build();
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error generating Agent's connection string.");
                return 1;
            }

            IContainer container;

            try
            {
                var builder = new ContainerBuilder();

                switch (configSourceConfig.ToLower())
                {
                    case "iothubconnected":
                        builder.RegisterModule(new IotHubConnectedModule(
                            dockerUri, dockerLoggingDriver, dockerLoggingOptions,
                            edgeHubConnectionDetails, deviceConnectionString,
                            backupConfigFilePath, maxRestartCount,
                            intensiveCareTime, coolOffTimeUnitInSeconds,
                            configuration, usePersistentStorage, storagePath, versionInfo));
                        break;
                    case "standalone":
                        builder.RegisterModule(new StandaloneModule(
                            dockerUri, dockerLoggingDriver, dockerLoggingOptions,
                            "config.json", maxRestartCount, intensiveCareTime,
                            coolOffTimeUnitInSeconds, configuration,
                            edgeHubConnectionDetails, deviceConnectionString,
                            usePersistentStorage, storagePath));
                        break;
                    default:
                        throw new Exception($"ConfigSource '{configSourceConfig}' not supported.");
                }

                container = builder.Build();

            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error building application.");
                return 1;
            }

            var cts = new CancellationTokenSource();

            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts, logger);

            int returnCode;
            using (IConfigSource unused = await container.Resolve<Task<IConfigSource>>())
            {
                Option<Agent> agentOption = Option.None<Agent>();

                try
                {

                    AssemblyLoadContext.Default.Unloading += OnUnload;
                    Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts, logger); };

                    Agent agent = await container.Resolve<Task<Agent>>();
                    agentOption = Option.Some(agent);
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

                    returnCode = 0;
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Main thread terminated");
                    AssemblyLoadContext.Default.Unloading -= OnUnload;
                    returnCode = 0;
                }
                catch (Exception ex)
                {
                    logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error starting Agent.");
                    returnCode = 1;
                }

                // Attempt to report shutdown of Agent
                await ReportShutdownAsync(agentOption, logger);
            }

            return returnCode;
        }

        static ILogger SetupLogger(IConfiguration configuration, string dockerLoggingDriver, Dictionary<string, string> dockerLoggingOptions)
        {
            string logLevel = configuration.GetValue($"{Logger.RuntimeLogLevelEnvKey}", "info");
            Logger.SetLogLevel(logLevel);

            var loggerBuilder = new ContainerBuilder();
            loggerBuilder.RegisterModule(
                new LoggingModule(
                    Preconditions.CheckNonWhiteSpace(dockerLoggingDriver, nameof(dockerLoggingDriver)),
                    Preconditions.CheckNotNull(dockerLoggingOptions, nameof(dockerLoggingOptions))));
            var loggerFactory = loggerBuilder.Build().Resolve<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger<Program>();

            return logger;
        }

        private static async Task ReportShutdownAsync(Option<Agent> agentOption, ILogger logger)
        {
            var closeCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            try
            {
                await agentOption.ForEachAsync((a) => a.ReportShutdownAsync(closeCts.Token));
            }
            catch (Exception ex)
            {
                logger.LogError(AgentEventIds.Agent, ex, "Error on shutdown");
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
