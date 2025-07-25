// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Agent.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;
    using StorageLogLevel = Microsoft.Azure.Devices.Edge.Storage.StorageLogLevel;

    public class Program
    {
        const string ConfigFileName = "appsettings_agent.json";
        const string DefaultLocalConfigFilePath = "config.json";
        const string EdgeAgentStorageFolder = "edgeAgent";
        const string EdgeAgentStorageBackupFolder = "edgeAgent_backup";
        const string VersionInfoFileName = "versionInfo.json";
        static readonly TimeSpan ShutdownWaitPeriod = TimeSpan.FromMinutes(1);
        static readonly TimeSpan ReconcileTimeout = TimeSpan.FromMinutes(10);

        public static int Main()
        {
            Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Edge Agent Main()");
            ILogger logger = null;

            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile(ConfigFileName)
                    .AddEnvironmentVariables()
                    .Build();

                // Bring up the logger before anything else so we can log errors ASAP
                logger = SetupLogger(configuration);

                if (configuration.GetValue<bool>("EnableSdkDebugLogs", false))
                {
                    // Enable SDK debug logs, see ConsoleEventListener for details.
                    string[] eventFilter = new string[] { "DotNetty-Default", "Microsoft-Azure-Devices", "Azure-Core", "Azure-Identity" };
                    using var sdk = new ConsoleEventListener(eventFilter, logger);

                    return MainAsync(configuration, logger).Result;
                }
                else
                {
                    return MainAsync(configuration, logger).Result;
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.LogDebug(ex, "An unhandled exception occurred");
                }
                else
                {
                    // Fallback if the logger hasn't been set up, should pretty much never happen
                    Console.Error.WriteLine(ex);
                }

                return 1;
            }
        }

        public static async Task<int> MainAsync(IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("Initializing Edge Agent.");

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
            bool enableNonPersistentStorageBackup;
            Option<string> storageBackupPath = Option.None<string>();
            string edgeDeviceHostName;
            IEnumerable<global::Docker.DotNet.Models.AuthConfig> dockerAuthConfig;
            int configRefreshFrequencySecs;
            ExperimentalFeatures experimentalFeatures;
            MetricsConfig metricsConfig;
            DiagnosticConfig diagnosticConfig;
            bool useServerHeartbeat;
            ModuleUpdateMode moduleUpdateMode;

            try
            {
                mode = configuration.GetValue(Constants.ModeKey, "iotedged");
                configSourceConfig = configuration.GetValue<string>("ConfigSource");
                backupConfigFilePath = configuration.GetValue<string>("BackupConfigFilePath");
                maxRestartCount = configuration.GetValue<int>("MaxRestartCount");
                intensiveCareTime = TimeSpan.FromMinutes(configuration.GetValue<int>("IntensiveCareTimeInMinutes"));
                coolOffTimeUnitInSeconds = configuration.GetValue("CoolOffTimeUnitInSeconds", 10);
                usePersistentStorage = configuration.GetValue("UsePersistentStorage", true);
                useServerHeartbeat = configuration.GetValue("UseServerHeartbeat", true);
                moduleUpdateMode = configuration.GetValue("ModuleUpdateMode", ModuleUpdateMode.NonBlocking);

                logger.LogInformation($"ModuleUpdateMode: {moduleUpdateMode.ToString()}");

                // Note: Keep in sync with iotedge-check's edge-agent-storage-mounted-from-host check (edgelet/iotedge/src/check/checks/storage_mounted_from_host.rs)
                storagePath = GetOrCreateDirectoryPath(configuration.GetValue<string>("StorageFolder"), EdgeAgentStorageFolder);
                enableNonPersistentStorageBackup = configuration.GetValue("EnableNonPersistentStorageBackup", false);

                if (enableNonPersistentStorageBackup)
                {
                    storageBackupPath = Option.Some(GetOrCreateDirectoryPath(configuration.GetValue<string>("BackupFolder"), EdgeAgentStorageBackupFolder));
                }

                backupConfigFilePath = GetFullBackupFilePath(storagePath, backupConfigFilePath);

                edgeDeviceHostName = configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
                dockerAuthConfig = configuration.GetSection("DockerRegistryAuth").Get<List<global::Docker.DotNet.Models.AuthConfig>>() ?? new List<global::Docker.DotNet.Models.AuthConfig>();

                NestedEdgeParentUriParser parser = new NestedEdgeParentUriParser();
                dockerAuthConfig = dockerAuthConfig.Select(c =>
                {
                    c.Password = parser.ParseURI(c.Password).GetOrElse(c.Password);
                    return c;
                })
                .ToList();

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
                builder.RegisterModule(new LoggingModule());
                string productInfo =
                    versionInfo != VersionInfo.Empty ?
                    $"{Constants.IoTEdgeAgentProductInfoIdentifier}/{versionInfo}" :
                    Constants.IoTEdgeAgentProductInfoIdentifier;
                Option<UpstreamProtocol> upstreamProtocol = configuration.GetValue<string>(Constants.UpstreamProtocolKey).ToUpstreamProtocol();
                Option<IWebProxy> proxy = Proxy.Parse(configuration.GetValue<string>("https_proxy"), logger);
                bool closeOnIdleTimeout = configuration.GetValue(Constants.CloseOnIdleTimeout, false);
                int idleTimeoutSecs = configuration.GetValue(Constants.IdleTimeoutSecs, 300);
                TimeSpan idleTimeout = TimeSpan.FromSeconds(idleTimeoutSecs);
                experimentalFeatures = ExperimentalFeatures.Create(configuration.GetSection("experimentalFeatures"), logger);
                Option<ulong> storageTotalMaxWalSize = GetConfigIfExists<ulong>(Constants.StorageMaxTotalWalSize, configuration, logger);
                Option<ulong> storageMaxManifestFileSize = GetConfigIfExists<ulong>(Constants.StorageMaxManifestFileSize, configuration, logger);
                Option<int> storageMaxOpenFiles = GetConfigIfExists<int>(Constants.StorageMaxOpenFiles, configuration, logger);
                Option<StorageLogLevel> storageLogLevel = GetConfigIfExists<StorageLogLevel>(Constants.StorageLogLevel, configuration, logger);
                string iothubHostname;
                string deviceId;
                string apiVersion = "2018-06-28";
                Option<X509Certificate2> manifestTrustBundle = Option.None<X509Certificate2>();
                int edgeletTimeoutSecs = configuration.GetValue(Constants.ManagementApiTimeoutSecs, 300);
                TimeSpan edgeletTimeout = TimeSpan.FromSeconds(edgeletTimeoutSecs);
                var enableOrphanedIdentityCleanup = configuration.GetValue("EnableOrphanedIdentityCleanup", false);
                int clientPermitTimeoutSecs = configuration.GetValue("ModuleRequestThrottleTimeout", 240);

                switch (mode.ToLowerInvariant())
                {
                    case Constants.IotedgedMode:
                        string managementUri = configuration.GetValue<string>(Constants.EdgeletManagementUriVariableName);
                        string workloadUri = configuration.GetValue<string>(Constants.EdgeletWorkloadUriVariableName);
                        bool disableDeviceAnalyticsMetadata = configuration.GetValue<bool?>("DisableDeviceAnalyticsMetadata") ?? configuration.GetValue<bool>("DisableDeviceAnalyticsTelemetry", false);
                        iothubHostname = configuration.GetValue<string>(Constants.IotHubHostnameVariableName);
                        deviceId = configuration.GetValue<string>(Constants.DeviceIdVariableName);
                        string moduleId = configuration.GetValue(Constants.ModuleIdVariableName, Constants.EdgeAgentModuleIdentityName);
                        string moduleGenerationId = configuration.GetValue<string>(Constants.EdgeletModuleGenerationIdVariableName);
                        apiVersion = configuration.GetValue<string>(Constants.EdgeletApiVersionVariableName);
                        TimeSpan performanceMetricsUpdateFrequency = configuration.GetTimeSpan("PerformanceMetricsUpdateFrequency", TimeSpan.FromMinutes(5));
                        builder.RegisterModule(new AgentModule(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath, Option.Some(new Uri(workloadUri)), Option.Some(apiVersion), moduleId, Option.Some(moduleGenerationId), enableNonPersistentStorageBackup, storageBackupPath, storageTotalMaxWalSize, storageMaxManifestFileSize, storageMaxOpenFiles, storageLogLevel, moduleUpdateMode));
                        builder.RegisterModule(new EdgeletModule(iothubHostname, deviceId, new Uri(managementUri), new Uri(workloadUri), apiVersion, dockerAuthConfig, upstreamProtocol, proxy, productInfo, closeOnIdleTimeout, idleTimeout, performanceMetricsUpdateFrequency, useServerHeartbeat, backupConfigFilePath, disableDeviceAnalyticsMetadata, moduleUpdateMode, edgeletTimeout, enableOrphanedIdentityCleanup, clientPermitTimeoutSecs));
                        IEnumerable<X509Certificate2> trustBundle =
                            await CertificateHelper.GetTrustBundleFromEdgelet(new Uri(workloadUri), apiVersion, Constants.WorkloadApiVersion, moduleId, moduleGenerationId);
                        CertificateHelper.InstallCertificates(trustBundle, logger);
                        manifestTrustBundle = await CertificateHelper.GetManifestTrustBundleFromEdgelet(new Uri(workloadUri), apiVersion, Constants.WorkloadApiVersion, moduleId, moduleGenerationId);

                        break;

                    case Constants.KubernetesMode:
                    default:
                        throw new InvalidOperationException($"Mode '{mode}' not supported.");
                }

                switch (configSourceConfig.ToLowerInvariant())
                {
                    case "twin":
                        bool enableStreams = configuration.GetValue(Constants.EnableStreams, false);
                        int requestTimeoutSecs = configuration.GetValue(Constants.RequestTimeoutSecs, 600);
                        builder.RegisterModule(
                            new TwinConfigSourceModule(
                                iothubHostname,
                                deviceId,
                                configuration,
                                versionInfo,
                                TimeSpan.FromSeconds(configRefreshFrequencySecs),
                                enableStreams,
                                TimeSpan.FromSeconds(requestTimeoutSecs),
                                experimentalFeatures,
                                manifestTrustBundle));
                        break;

                    case "local":
                        string localConfigFilePath = GetLocalConfigFilePath(configuration, logger);
                        builder.RegisterModule(new FileConfigSourceModule(localConfigFilePath, configuration, versionInfo));
                        break;

                    default:
                        throw new InvalidOperationException($"ConfigSource '{configSourceConfig}' not supported.");
                }

                metricsConfig = new MetricsConfig(configuration);
                builder.RegisterModule(new MetricsModule(metricsConfig, iothubHostname, deviceId));

                bool diagnosticsEnabled = configuration.GetValue("SendRuntimeQualityTelemetry", true);
                diagnosticConfig = new DiagnosticConfig(diagnosticsEnabled, storagePath, configuration);
                builder.RegisterModule(new DiagnosticsModule(diagnosticConfig));

                container = builder.Build();
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error building application.");
                return 1;
            }

            // Initialize metrics
            if (metricsConfig.Enabled)
            {
                container.Resolve<IMetricsListener>().Start(logger);
                container.Resolve<ISystemResourcesMetrics>().Start(logger);
                await container.Resolve<MetadataMetrics>().Start(logger, versionInfo.ToString(true), Newtonsoft.Json.JsonConvert.SerializeObject(experimentalFeatures));
            }

            // Initialize metric uploading
            if (diagnosticConfig.Enabled)
            {
                MetricsWorker worker = await container.Resolve<Task<MetricsWorker>>();
                worker.Start(diagnosticConfig.ScrapeInterval, diagnosticConfig.UploadInterval);
                Console.WriteLine($"Scraping frequency: {diagnosticConfig.ScrapeInterval}\nUpload Frequency: {diagnosticConfig.UploadInterval}");
            }

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler)
                = ShutdownHandler.Init(ShutdownWaitPeriod, logger);

            // Register request handlers
            await RegisterRequestHandlers(container);

            // Initialize stream request listener
            IStreamRequestListener streamRequestListener = await container.Resolve<Task<IStreamRequestListener>>();
            streamRequestListener.InitPump();

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
                            await agent.ReconcileAsync(cts.Token).TimeoutAfter(ReconcileTimeout);
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
                await CloseDbStoreProviderAsync(container);

                if (metricsConfig.Enabled && returnCode == 0)
                {
                    container.Resolve<IDeploymentMetrics>().IndicateCleanShutdown();
                }

                completed.Set();
            }

            handler.ForEach(h => GC.KeepAlive(h));
            return returnCode;
        }

        static async Task RegisterRequestHandlers(IContainer container)
        {
            var requestHandlerTasks = container.Resolve<IEnumerable<Task<IRequestHandler>>>();
            IRequestHandler[] requestHandlers = await Task.WhenAll(requestHandlerTasks);
            IRequestManager requestManager = container.Resolve<IRequestManager>();
            requestManager.RegisterHandlers(requestHandlers);
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

        static string GetOrCreateDirectoryPath(string baseDirectoryPath, string directoryName)
        {
            if (string.IsNullOrWhiteSpace(baseDirectoryPath) || !Directory.Exists(baseDirectoryPath))
            {
                baseDirectoryPath = Path.GetTempPath();
            }

            string directoryPath = Path.Combine(baseDirectoryPath, directoryName);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return directoryPath;
        }

        static string GetLocalConfigFilePath(IConfiguration configuration, ILogger logger)
        {
            string localConfigPath = configuration.GetValue<string>("LocalConfigPath");

            if (string.IsNullOrWhiteSpace(localConfigPath))
            {
                logger.LogInformation("No local config path specified. Using default path instead.");
                localConfigPath = DefaultLocalConfigFilePath;
            }

            logger.LogInformation($"Local config path: {localConfigPath}");
            return localConfigPath;
        }

        static string GetFullBackupFilePath(string storageFolder, string backupFilePath)
        {
            if (Path.IsPathRooted(backupFilePath))
            {
                return backupFilePath;
            }

            return Path.Join(storageFolder, backupFilePath);
        }

        static async Task CloseDbStoreProviderAsync(IContainer container)
        {
            IDbStoreProvider dbStoreProvider = await container.Resolve<Task<IDbStoreProvider>>();
            await dbStoreProvider.CloseAsync();
        }

        // TODO: Move this function to a common location that can be shared between EdgeHub and EdgeAgent
        static Option<T> GetConfigIfExists<T>(string fieldName, IConfiguration configuration, ILogger logger = default(ILogger))
        {
            T storageParamValue = default(T);
            try
            {
                storageParamValue = configuration.GetValue<T>(fieldName);
            }
            catch
            {
                logger?.LogError($"Cannot get parameter '{fieldName}' from the config");
            }

            return EqualityComparer<T>.Default.Equals(storageParamValue, default(T)) ? Option.None<T>() : Option.Some(storageParamValue);
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
