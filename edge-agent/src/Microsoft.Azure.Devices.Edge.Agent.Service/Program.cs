// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class Program
    {
        const string ConfigFileName = "appsettings_agent.json";
        const string EdgeAgentStorageFolder = "edgeAgent";
        const string VersionInfoFileName = "versionInfo.json";
        static readonly TimeSpan ShutdownWaitPeriod = TimeSpan.FromMinutes(1);

        public static int Main()
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}] Edge Agent Main()");
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
            // Bring up the logger before anything else so we can log errors ASAP
            ILogger logger = SetupLogger(configuration);

            logger.LogInformation("Starting module management agent.");

            VersionInfo versionInfo = VersionInfo.Get(VersionInfoFileName);
            if (versionInfo != VersionInfo.Empty)
            {
                logger.LogInformation($"Version - {versionInfo.ToString(true)}");
            }

            LogLogo(logger);

            string mode;

            string configSourceConfig;
            string backupConfigFilePath;
            int maxRestartCount;
            TimeSpan intensiveCareTime;
            int coolOffTimeUnitInSeconds;
            bool usePersistentStorage;
            string storagePath;
            string edgeDeviceHostName;
            string dockerLoggingDriver;
            Dictionary<string, string> dockerLoggingOptions;
            IEnumerable<AuthConfig> dockerAuthConfig;
            int configRefreshFrequencySecs;

            try
            {
                mode = configuration.GetValue(Constants.ModeKey, "docker");
                configSourceConfig = configuration.GetValue<string>("ConfigSource");
                backupConfigFilePath = configuration.GetValue<string>("BackupConfigFilePath");
                maxRestartCount = configuration.GetValue<int>("MaxRestartCount");
                intensiveCareTime = TimeSpan.FromMinutes(configuration.GetValue<int>("IntensiveCareTimeInMinutes"));
                coolOffTimeUnitInSeconds = configuration.GetValue("CoolOffTimeUnitInSeconds", 10);
                usePersistentStorage = configuration.GetValue("UsePersistentStorage", true);
                storagePath = GetStoragePath(configuration);
                edgeDeviceHostName = configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
                dockerLoggingDriver = configuration.GetValue<string>("DockerLoggingDriver");
                dockerLoggingOptions = configuration.GetSection("DockerLoggingOptions").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                dockerAuthConfig = configuration.GetSection("DockerRegistryAuth").Get<List<AuthConfig>>() ?? new List<AuthConfig>();
                configRefreshFrequencySecs = configuration.GetValue("ConfigRefreshFrequencySecs", 3600);
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error reading the Agent's configuration.");
                return 1;
            }

            IContainer container;
            try
            {
                var builder = new ContainerBuilder();
                builder.RegisterModule(new LoggingModule(dockerLoggingDriver, dockerLoggingOptions));
                Option<string> productInfo = versionInfo != VersionInfo.Empty ? Option.Some(versionInfo.ToString()) : Option.None<string>();
                Option<UpstreamProtocol> upstreamProtocol = configuration.GetValue<string>(Constants.UpstreamProtocolKey).ToUpstreamProtocol();
                Option<IWebProxy> proxy = Proxy.Parse(configuration.GetValue<string>("https_proxy"), logger);
                switch (mode.ToLowerInvariant())
                {
                    case Constants.DockerMode:
                        var dockerUri = new Uri(configuration.GetValue<string>("DockerUri"));
                        string deviceConnectionString = configuration.GetValue<string>("DeviceConnectionString");
                        builder.RegisterModule(new AgentModule(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath));
                        builder.RegisterModule(new DockerModule(deviceConnectionString, edgeDeviceHostName, dockerUri, dockerAuthConfig, upstreamProtocol, proxy, productInfo));
                        break;

                    case Constants.IotedgedMode:
                        string managementUri = configuration.GetValue<string>(Constants.EdgeletManagementUriVariableName);
                        string workloadUri = configuration.GetValue<string>(Constants.EdgeletWorkloadUriVariableName);
                        string iothubHostname = configuration.GetValue<string>(Constants.IotHubHostnameVariableName);
                        string deviceId = configuration.GetValue<string>(Constants.DeviceIdVariableName);
                        string moduleId = configuration.GetValue(Constants.ModuleIdVariableName, Constants.EdgeAgentModuleIdentityName);
                        string moduleGenerationId = configuration.GetValue<string>(Constants.EdgeletModuleGenerationIdVariableName);
                        string apiVersion = configuration.GetValue<string>(Constants.EdgeletApiVersionVariableName);
                        builder.RegisterModule(new AgentModule(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath, Option.Some(new Uri(workloadUri)), Option.Some(apiVersion), moduleId, Option.Some(moduleGenerationId)));
                        builder.RegisterModule(new EdgeletModule(iothubHostname, edgeDeviceHostName, deviceId, new Uri(managementUri), new Uri(workloadUri), apiVersion, dockerAuthConfig, upstreamProtocol, proxy, productInfo));
                        break;

                    default:
                        throw new InvalidOperationException($"Mode '{mode}' not supported.");
                }

                switch (configSourceConfig.ToLowerInvariant())
                {
                    case "twin":
                        builder.RegisterModule(new TwinConfigSourceModule(backupConfigFilePath, configuration, versionInfo, TimeSpan.FromSeconds(configRefreshFrequencySecs)));
                        break;

                    case "local":
                        builder.RegisterModule(new FileConfigSourceModule("config.json", configuration));
                        break;

                    default:
                        throw new InvalidOperationException($"ConfigSource '{configSourceConfig}' not supported.");
                }

                container = builder.Build();
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error building application.");
                return 1;
            }

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler)
                = ShutdownHandler.Init(ShutdownWaitPeriod, logger);

            int returnCode;
            using (IConfigSource unused = await container.Resolve<Task<IConfigSource>>())
            {
                Option<Agent> agentOption = Option.None<Agent>();

                try
                {
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

                    logger.LogInformation("Closing module management agent.");

                    returnCode = 0;
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Main thread terminated");
                    returnCode = 0;
                }
                catch (Exception ex)
                {
                    logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error starting Agent.");
                    returnCode = 1;
                }

                // Attempt to report shutdown of Agent
                await Cleanup(agentOption, logger);
                completed.Set();
            }

            handler.ForEach(h => GC.KeepAlive(h));
            return returnCode;
        }

        static ILogger SetupLogger(IConfiguration configuration)
        {
            string logLevel = configuration.GetValue($"{Logger.RuntimeLogLevelEnvKey}", "info");
            Logger.SetLogLevel(logLevel);
            ILogger logger = Logger.Factory.CreateLogger<Program>();
            return logger;
        }

        static Task Cleanup(Option<Agent> agentOption, ILogger logger)
        {
            var closeCts = new CancellationTokenSource(ShutdownWaitPeriod);

            try
            {
                return agentOption.ForEachAsync(a => a.HandleShutdown(closeCts.Token));
            }
            catch (Exception ex)
            {
                logger.LogError(AgentEventIds.Agent, ex, "Error on shutdown");
                return Task.CompletedTask;
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
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
            }

            return storagePath;
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
