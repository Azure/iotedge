// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
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
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Service;
    using Microsoft.Azure.Devices.Edge.Agent.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Core.Constants;
    using K8sConstants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;
    using KubernetesModule = Microsoft.Azure.Devices.Edge.Agent.Service.Modules.KubernetesModule;
    using MetricsListener = Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net.MetricsListener;

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
            string dockerLoggingDriver;
            Dictionary<string, string> dockerLoggingOptions;
            IEnumerable<global::Docker.DotNet.Models.AuthConfig> dockerAuthConfig;
            int configRefreshFrequencySecs;
            ExperimentalFeatures experimentalFeatures;
            MetricsConfig metricsConfig;
            DiagnosticConfig diagnosticConfig;

            try
            {
                mode = configuration.GetValue(Constants.ModeKey, "docker");
                configSourceConfig = configuration.GetValue<string>("ConfigSource");
                backupConfigFilePath = configuration.GetValue<string>("BackupConfigFilePath");
                maxRestartCount = configuration.GetValue<int>("MaxRestartCount");
                intensiveCareTime = TimeSpan.FromMinutes(configuration.GetValue<int>("IntensiveCareTimeInMinutes"));
                coolOffTimeUnitInSeconds = configuration.GetValue("CoolOffTimeUnitInSeconds", 10);
                usePersistentStorage = configuration.GetValue("UsePersistentStorage", true);

                // Note: Keep in sync with iotedge-check's edge-agent-storage-mounted-from-host check (edgelet/iotedge/src/check/checks/storage_mounted_from_host.rs)
                storagePath = GetOrCreateDirectoryPath(configuration.GetValue<string>("StorageFolder"), EdgeAgentStorageFolder);
                enableNonPersistentStorageBackup = configuration.GetValue("EnableNonPersistentStorageBackup", false);

                if (enableNonPersistentStorageBackup)
                {
                    storageBackupPath = Option.Some(GetOrCreateDirectoryPath(configuration.GetValue<string>("BackupFolder"), EdgeAgentStorageBackupFolder));
                }

                edgeDeviceHostName = configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
                dockerLoggingDriver = configuration.GetValue<string>("DockerLoggingDriver");
                dockerLoggingOptions = configuration.GetSection("DockerLoggingOptions").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                dockerAuthConfig = configuration.GetSection("DockerRegistryAuth").Get<List<global::Docker.DotNet.Models.AuthConfig>>() ?? new List<global::Docker.DotNet.Models.AuthConfig>();
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
                string productInfo = versionInfo != VersionInfo.Empty ? $"{Constants.IoTEdgeAgentProductInfoIdentifier}/{versionInfo}" : Constants.IoTEdgeAgentProductInfoIdentifier;
                Option<UpstreamProtocol> upstreamProtocol = configuration.GetValue<string>(Constants.UpstreamProtocolKey).ToUpstreamProtocol();
                Option<IWebProxy> proxy = Proxy.Parse(configuration.GetValue<string>("https_proxy"), logger);
                bool closeOnIdleTimeout = configuration.GetValue(Constants.CloseOnIdleTimeout, false);
                int idleTimeoutSecs = configuration.GetValue(Constants.IdleTimeoutSecs, 300);
                TimeSpan idleTimeout = TimeSpan.FromSeconds(idleTimeoutSecs);
                experimentalFeatures = ExperimentalFeatures.Create(configuration.GetSection("experimentalFeatures"), logger);
                Option<ulong> storageTotalMaxWalSize = GetStorageMaxTotalWalSizeIfExists(configuration);
                string iothubHostname;
                string deviceId;
                string apiVersion = "2018-06-28";

                switch (mode.ToLowerInvariant())
                {
                    case Constants.DockerMode:
                        var dockerUri = new Uri(configuration.GetValue<string>("DockerUri"));
                        string deviceConnectionString = configuration.GetValue<string>("DeviceConnectionString");
                        IotHubConnectionStringBuilder connectionStringParser = IotHubConnectionStringBuilder.Create(deviceConnectionString);
                        deviceId = connectionStringParser.DeviceId;
                        iothubHostname = connectionStringParser.HostName;
                        builder.RegisterModule(new AgentModule(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath, enableNonPersistentStorageBackup, storageBackupPath, storageTotalMaxWalSize));
                        builder.RegisterModule(new DockerModule(deviceConnectionString, edgeDeviceHostName, dockerUri, dockerAuthConfig, upstreamProtocol, proxy, productInfo, closeOnIdleTimeout, idleTimeout));
                        break;

                    case Constants.IotedgedMode:
                        string managementUri = configuration.GetValue<string>(Constants.EdgeletManagementUriVariableName);
                        string workloadUri = configuration.GetValue<string>(Constants.EdgeletWorkloadUriVariableName);
                        iothubHostname = configuration.GetValue<string>(Constants.IotHubHostnameVariableName);
                        deviceId = configuration.GetValue<string>(Constants.DeviceIdVariableName);
                        string moduleId = configuration.GetValue(Constants.ModuleIdVariableName, Constants.EdgeAgentModuleIdentityName);
                        string moduleGenerationId = configuration.GetValue<string>(Constants.EdgeletModuleGenerationIdVariableName);
                        apiVersion = configuration.GetValue<string>(Constants.EdgeletApiVersionVariableName);
                        TimeSpan performanceMetricsUpdateFrequency = configuration.GetValue("PerformanceMetricsUpdateFrequency", TimeSpan.FromSeconds(30));
                        builder.RegisterModule(new AgentModule(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath, Option.Some(new Uri(workloadUri)), Option.Some(apiVersion), moduleId, Option.Some(moduleGenerationId), enableNonPersistentStorageBackup, storageBackupPath, storageTotalMaxWalSize));
                        builder.RegisterModule(new EdgeletModule(iothubHostname, edgeDeviceHostName, deviceId, new Uri(managementUri), new Uri(workloadUri), apiVersion, dockerAuthConfig, upstreamProtocol, proxy, productInfo, closeOnIdleTimeout, idleTimeout, performanceMetricsUpdateFrequency));

                        IEnumerable<X509Certificate2> trustBundle = await CertificateHelper.GetTrustBundleFromEdgelet(new Uri(workloadUri), apiVersion, Constants.WorkloadApiVersion, moduleId, moduleGenerationId);
                        CertificateHelper.InstallCertificates(trustBundle, logger);

                        break;

                    case Constants.KubernetesMode:
                        managementUri = configuration.GetValue<string>(Constants.EdgeletManagementUriVariableName);
                        workloadUri = configuration.GetValue<string>(Constants.EdgeletWorkloadUriVariableName);
                        moduleId = configuration.GetValue(Constants.ModuleIdVariableName, Constants.EdgeAgentModuleIdentityName);
                        moduleGenerationId = configuration.GetValue<string>(Constants.EdgeletModuleGenerationIdVariableName);
                        apiVersion = configuration.GetValue<string>(Constants.EdgeletApiVersionVariableName);
                        iothubHostname = configuration.GetValue<string>(Constants.IotHubHostnameVariableName);
                        deviceId = configuration.GetValue<string>(Constants.DeviceIdVariableName);
                        string proxyImage = configuration.GetValue<string>(K8sConstants.ProxyImageEnvKey);
                        Option<string> proxyImagePullSecretName = Option.Maybe(configuration.GetValue<string>(K8sConstants.ProxyImagePullSecretNameEnvKey));
                        string proxyConfigPath = configuration.GetValue<string>(K8sConstants.ProxyConfigPathEnvKey);
                        string proxyConfigVolumeName = configuration.GetValue<string>(K8sConstants.ProxyConfigVolumeEnvKey);
                        string proxyConfigMapName = configuration.GetValue<string>(K8sConstants.ProxyConfigMapNameEnvKey);
                        string proxyTrustBundlePath = configuration.GetValue<string>(K8sConstants.ProxyTrustBundlePathEnvKey);
                        string proxyTrustBundleVolumeName = configuration.GetValue<string>(K8sConstants.ProxyTrustBundleVolumeEnvKey);
                        string proxyTrustBundleConfigMapName = configuration.GetValue<string>(K8sConstants.ProxyTrustBundleConfigMapEnvKey);
                        PortMapServiceType mappedServiceDefault = GetDefaultServiceType(configuration);
                        bool enableServiceCallTracing = configuration.GetValue<bool>(K8sConstants.EnableK8sServiceCallTracingName);
                        string persistentVolumeName = configuration.GetValue<string>(K8sConstants.PersistentVolumeNameKey);
                        string storageClassName = configuration.GetValue<string>(K8sConstants.StorageClassNameKey);
                        Option<uint> persistentVolumeClaimDefaultSizeMb = Option.Maybe(configuration.GetValue<uint?>(K8sConstants.PersistentVolumeClaimDefaultSizeInMbKey));
                        string deviceNamespace = configuration.GetValue<string>(K8sConstants.K8sNamespaceKey);
                        var kubernetesExperimentalFeatures = KubernetesExperimentalFeatures.Create(configuration.GetSection("experimentalFeatures"), logger);
                        var moduleOwner = new KubernetesModuleOwner(
                            configuration.GetValue<string>(K8sConstants.EdgeK8sObjectOwnerApiVersionKey),
                            configuration.GetValue<string>(K8sConstants.EdgeK8sObjectOwnerKindKey),
                            configuration.GetValue<string>(K8sConstants.EdgeK8sObjectOwnerNameKey),
                            configuration.GetValue<string>(K8sConstants.EdgeK8sObjectOwnerUidKey));
                        bool runAsNonRoot = configuration.GetValue<bool>(K8sConstants.RunAsNonRootKey);

                        builder.RegisterModule(new AgentModule(maxRestartCount, intensiveCareTime, coolOffTimeUnitInSeconds, usePersistentStorage, storagePath, Option.Some(new Uri(workloadUri)), Option.Some(apiVersion), moduleId, Option.Some(moduleGenerationId), enableNonPersistentStorageBackup, storageBackupPath, storageTotalMaxWalSize));
                        builder.RegisterModule(new KubernetesModule(
                            iothubHostname,
                            deviceId,
                            edgeDeviceHostName,
                            proxyImage,
                            proxyImagePullSecretName,
                            proxyConfigPath,
                            proxyConfigVolumeName,
                            proxyConfigMapName,
                            proxyTrustBundlePath,
                            proxyTrustBundleVolumeName,
                            proxyTrustBundleConfigMapName,
                            apiVersion,
                            deviceNamespace,
                            new Uri(managementUri),
                            new Uri(workloadUri),
                            dockerAuthConfig,
                            upstreamProtocol,
                            Option.Some(productInfo),
                            mappedServiceDefault,
                            enableServiceCallTracing,
                            persistentVolumeName,
                            storageClassName,
                            persistentVolumeClaimDefaultSizeMb,
                            proxy,
                            closeOnIdleTimeout,
                            idleTimeout,
                            kubernetesExperimentalFeatures,
                            moduleOwner,
                            runAsNonRoot));

                        break;

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
                                backupConfigFilePath,
                                configuration,
                                versionInfo,
                                TimeSpan.FromSeconds(configRefreshFrequencySecs),
                                enableStreams,
                                TimeSpan.FromSeconds(requestTimeoutSecs),
                                experimentalFeatures));
                        break;

                    case "local":
                        string localConfigFilePath = GetLocalConfigFilePath(configuration, logger);
                        builder.RegisterModule(new FileConfigSourceModule(localConfigFilePath, configuration, versionInfo));
                        break;

                    default:
                        throw new InvalidOperationException($"ConfigSource '{configSourceConfig}' not supported.");
                }

                metricsConfig = new MetricsConfig(experimentalFeatures.EnableMetrics, MetricsListenerConfig.Create(configuration));
                builder.RegisterModule(new MetricsModule(metricsConfig, iothubHostname, deviceId));

                diagnosticConfig = new DiagnosticConfig(experimentalFeatures.EnableMetricsUpload, storagePath, configuration);
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
                container.Resolve<SystemResourcesMetrics>().Start(logger);
            }

            // Initialize metric uploading
            if (diagnosticConfig.Enabled)
            {
                MetricsWorker worker = container.Resolve<MetricsWorker>();
                worker.Start(diagnosticConfig.ScrapeInterval, diagnosticConfig.UploadInterval);
                Console.WriteLine($"Scraping frequency: {diagnosticConfig.ScrapeInterval}\nUpload Frequency: {diagnosticConfig.UploadInterval}");
            }

            // TODO move this code to Agent
            if (mode.ToLowerInvariant().Equals(Constants.KubernetesMode))
            {
                // Start environment operator
                IKubernetesEnvironmentOperator environmentOperator = container.Resolve<IKubernetesEnvironmentOperator>();
                environmentOperator.Start();

                // Start the edge deployment operator
                IEdgeDeploymentOperator edgeDeploymentOperator = container.Resolve<IEdgeDeploymentOperator>();
                edgeDeploymentOperator.Start();
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
                    container.Resolve<IAvailabilityMetric>().IndicateCleanShutdown();
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

        static PortMapServiceType GetDefaultServiceType(IConfiguration configuration) =>
            Enum.TryParse(configuration.GetValue(K8sConstants.PortMappingServiceType, string.Empty), true, out PortMapServiceType defaultServiceType)
                ? defaultServiceType
                : Kubernetes.Constants.DefaultPortMapServiceType;

        static async Task CloseDbStoreProviderAsync(IContainer container)
        {
            IDbStoreProvider dbStoreProvider = await container.Resolve<Task<IDbStoreProvider>>();
            await dbStoreProvider.CloseAsync();
        }

        static Option<ulong> GetStorageMaxTotalWalSizeIfExists(IConfiguration configuration)
        {
            ulong storageMaxTotalWalSize = 0;
            try
            {
                storageMaxTotalWalSize = configuration.GetValue<ulong>(Constants.StorageMaxTotalWalSize);
            }
            catch
            {
                // ignored
            }

            return storageMaxTotalWalSize <= 0 ? Option.None<ulong>() : Option.Some(storageMaxTotalWalSize);
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
