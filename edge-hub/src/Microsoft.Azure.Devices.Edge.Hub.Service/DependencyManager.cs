// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class DependencyManager : IDependencyManager
    {
        readonly IConfigurationRoot configuration;
        readonly X509Certificate2 serverCertificate;
        readonly IList<X509Certificate2> trustBundle;

        readonly string iotHubHostname;
        readonly string edgeDeviceId;
        readonly string edgeModuleId;
        readonly string edgeDeviceHostName;
        readonly Option<string> connectionString;
        readonly VersionInfo versionInfo;

        public DependencyManager(IConfigurationRoot configuration, X509Certificate2 serverCertificate, IList<X509Certificate2> trustBundle)
        {
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.serverCertificate = Preconditions.CheckNotNull(serverCertificate, nameof(serverCertificate));
            this.trustBundle = Preconditions.CheckNotNull(trustBundle, nameof(trustBundle));

            string edgeHubConnectionString = this.configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);
            if (!string.IsNullOrWhiteSpace(edgeHubConnectionString))
            {
                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
                this.iotHubHostname = iotHubConnectionStringBuilder.HostName;
                this.edgeDeviceId = iotHubConnectionStringBuilder.DeviceId;
                this.edgeModuleId = iotHubConnectionStringBuilder.ModuleId;
                this.edgeDeviceHostName = this.configuration.GetValue(Constants.ConfigKey.EdgeDeviceHostName, string.Empty);
                this.connectionString = Option.Some(edgeHubConnectionString);
            }
            else
            {
                this.iotHubHostname = this.configuration.GetValue<string>(Constants.ConfigKey.IotHubHostname);
                this.edgeDeviceId = this.configuration.GetValue<string>(Constants.ConfigKey.DeviceId);
                this.edgeModuleId = this.configuration.GetValue<string>(Constants.ConfigKey.ModuleId);
                this.edgeDeviceHostName = this.configuration.GetValue<string>(Constants.ConfigKey.EdgeDeviceHostName);
                this.connectionString = Option.None<string>();
            }

            this.versionInfo = VersionInfo.Get(Constants.VersionInfoFileName);
        }

        public void Register(ContainerBuilder builder)
        {
            builder.RegisterModule(new LoggingModule());
            builder.RegisterBuildCallback(
                c =>
                {
                    // set up loggers for Dotnetty
                    var loggerFactory = c.Resolve<ILoggerFactory>();
                    InternalLoggerFactory.DefaultFactory = loggerFactory;

                    var eventListener = new LoggerEventListener(loggerFactory.CreateLogger("ProtocolGateway"));
                    eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Informational);
                });

            bool optimizeForPerformance = this.configuration.GetValue("OptimizeForPerformance", true);
            (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward = this.GetStoreAndForwardConfiguration();

            this.RegisterCommonModule(builder, optimizeForPerformance, storeAndForward);
            this.RegisterRoutingModule(builder, storeAndForward);
            this.RegisterMqttModule(builder, storeAndForward, optimizeForPerformance);
            this.RegisterAmqpModule(builder);
            builder.RegisterModule(new HttpModule());
        }

        internal static Option<UpstreamProtocol> GetUpstreamProtocol(IConfigurationRoot configuration) =>
            Enum.TryParse(configuration.GetValue("UpstreamProtocol", string.Empty), true, out UpstreamProtocol upstreamProtocol)
                ? Option.Some(upstreamProtocol)
                : Option.None<UpstreamProtocol>();

        void RegisterAmqpModule(ContainerBuilder builder)
        {
            IConfiguration amqpSettings = this.configuration.GetSection("amqpSettings");
            bool clientCertAuthEnabled = this.configuration.GetValue(Constants.ConfigKey.EdgeHubClientCertAuthEnabled, false);
            builder.RegisterModule(new AmqpModule(amqpSettings["scheme"], amqpSettings.GetValue<ushort>("port"), this.serverCertificate, this.iotHubHostname, clientCertAuthEnabled));
        }

        void RegisterMqttModule(ContainerBuilder builder, (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward, bool optimizeForPerformance)
        {
            var topics = new MessageAddressConversionConfiguration(
                this.configuration.GetSection(Constants.TopicNameConversionSectionName + ":InboundTemplates").Get<List<string>>(),
                this.configuration.GetSection(Constants.TopicNameConversionSectionName + ":OutboundTemplates").Get<Dictionary<string, string>>());

            bool clientCertAuthEnabled = this.configuration.GetValue(Constants.ConfigKey.EdgeHubClientCertAuthEnabled, false);

            IConfiguration mqttSettingsConfiguration = this.configuration.GetSection("mqttSettings");
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration, topics, this.serverCertificate, storeAndForward.isEnabled, clientCertAuthEnabled, optimizeForPerformance));
        }

        void RegisterRoutingModule(ContainerBuilder builder, (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward)
        {
            var routes = this.configuration.GetSection("routes").Get<Dictionary<string, string>>();
            int connectionPoolSize = this.configuration.GetValue<int>("IotHubConnectionPoolSize");
            string configSource = this.configuration.GetValue<string>("configSource");
            bool useTwinConfig = !string.IsNullOrWhiteSpace(configSource) && configSource.Equals("twin", StringComparison.OrdinalIgnoreCase);
            Option<UpstreamProtocol> upstreamProtocolOption = GetUpstreamProtocol(this.configuration);
            int connectivityCheckFrequencySecs = this.configuration.GetValue("ConnectivityCheckFrequencySecs", 300);
            TimeSpan connectivityCheckFrequency = connectivityCheckFrequencySecs < 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(connectivityCheckFrequencySecs);
            // n Clients + 1 Edgehub
            int maxConnectedClients = this.configuration.GetValue("MaxConnectedClients", 100) + 1;
            int cloudConnectionIdleTimeoutSecs = this.configuration.GetValue("CloudConnectionIdleTimeoutSecs", 3600);
            TimeSpan cloudConnectionIdleTimeout = TimeSpan.FromSeconds(cloudConnectionIdleTimeoutSecs);
            bool closeCloudConnectionOnIdleTimeout = this.configuration.GetValue("CloseCloudConnectionOnIdleTimeout", true);
            int cloudOperationTimeoutSecs = this.configuration.GetValue("CloudOperationTimeoutSecs", 20);
            TimeSpan cloudOperationTimeout = TimeSpan.FromSeconds(cloudOperationTimeoutSecs);
            Option<TimeSpan> minTwinSyncPeriod = this.GetConfigurationValueIfExists("MinTwinSyncPeriodSecs")
                .Map(s => TimeSpan.FromSeconds(s));
            Option<TimeSpan> reportedPropertiesSyncFrequency = this.GetConfigurationValueIfExists("ReportedPropertiesSyncFrequencySecs")
                .Map(s => TimeSpan.FromSeconds(s));
            bool useV1TwinManager = this.GetConfigurationValueIfExists<string>("TwinManagerVersion")
                .Map(v => v.Equals("v1", StringComparison.OrdinalIgnoreCase))
                .GetOrElse(false);
            int maxUpstreamBatchSize = this.configuration.GetValue("MaxUpstreamBatchSize", 10);
            int upstreamFanOutFactor = this.configuration.GetValue("UpstreamFanOutFactor", 10);
            bool encryptTwinStore = this.configuration.GetValue("EncryptTwinStore", false);
            bool disableCloudSubscriptions = this.configuration.GetValue("DisableCloudSubscriptions", false);
            bool enableConnectivityCheck = this.configuration.GetValue("EnableConnectivityCheck", true);
            int configUpdateFrequencySecs = this.configuration.GetValue("ConfigRefreshFrequencySecs", 3600);
            TimeSpan configUpdateFrequency = TimeSpan.FromSeconds(configUpdateFrequencySecs);

            builder.RegisterModule(
                new RoutingModule(
                    this.iotHubHostname,
                    this.edgeDeviceId,
                    this.edgeModuleId,
                    this.connectionString,
                    routes,
                    storeAndForward.isEnabled,
                    storeAndForward.config,
                    connectionPoolSize,
                    useTwinConfig,
                    this.versionInfo,
                    upstreamProtocolOption,
                    connectivityCheckFrequency,
                    maxConnectedClients,
                    cloudConnectionIdleTimeout,
                    closeCloudConnectionOnIdleTimeout,
                    cloudOperationTimeout,
                    minTwinSyncPeriod,
                    reportedPropertiesSyncFrequency,
                    useV1TwinManager,
                    maxUpstreamBatchSize,
                    upstreamFanOutFactor,
                    encryptTwinStore,
                    disableCloudSubscriptions,
                    configUpdateFrequency,
                    enableConnectivityCheck));
        }

        void RegisterCommonModule(ContainerBuilder builder, bool optimizeForPerformance, (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward)
        {
            bool cacheTokens = this.configuration.GetValue("CacheTokens", false);
            Option<string> workloadUri = this.GetConfigurationValueIfExists<string>(Constants.ConfigKey.WorkloadUri);
            Option<string> workloadApiVersion = this.GetConfigurationValueIfExists<string>(Constants.ConfigKey.WorkloadAPiVersion);
            Option<string> moduleGenerationId = this.GetConfigurationValueIfExists<string>(Constants.ConfigKey.ModuleGenerationId);

            if (!Enum.TryParse(this.configuration.GetValue("AuthenticationMode", string.Empty), true, out AuthenticationMode authenticationMode))
            {
                authenticationMode = AuthenticationMode.CloudAndScope;
            }

            int scopeCacheRefreshRateSecs = this.configuration.GetValue("DeviceScopeCacheRefreshRateSecs", 3600);
            TimeSpan scopeCacheRefreshRate = TimeSpan.FromSeconds(scopeCacheRefreshRateSecs);

            string proxy = this.configuration.GetValue("https_proxy", string.Empty);
            string productInfo = GetProductInfo();

            // Register modules
            builder.RegisterModule(
                new CommonModule(
                    productInfo,
                    this.iotHubHostname,
                    this.edgeDeviceId,
                    this.edgeModuleId,
                    this.edgeDeviceHostName,
                    moduleGenerationId,
                    authenticationMode,
                    this.connectionString,
                    optimizeForPerformance,
                    storeAndForward.usePersistentStorage,
                    storeAndForward.storagePath,
                    workloadUri,
                    workloadApiVersion,
                    scopeCacheRefreshRate,
                    cacheTokens,
                    this.trustBundle,
                    proxy));
        }

        static string GetProductInfo()
        {
            string version = VersionInfo.Get(Constants.VersionInfoFileName).ToString();
            string productInfo = $"{Core.Constants.IoTEdgeProductInfoIdentifier}/{version}";
            return productInfo;
        }

        (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) GetStoreAndForwardConfiguration()
        {
            int defaultTtl = -1;
            bool usePersistentStorage = this.configuration.GetValue<bool>("usePersistentStorage");
            int timeToLiveSecs = defaultTtl;
            string storagePath = this.GetStoragePath();
            bool storeAndForwardEnabled = this.configuration.GetValue<bool>("storeAndForwardEnabled");
            if (storeAndForwardEnabled)
            {
                IConfiguration storeAndForwardConfigurationSection = this.configuration.GetSection("storeAndForward");
                timeToLiveSecs = storeAndForwardConfigurationSection.GetValue("timeToLiveSecs", defaultTtl);
            }

            var storeAndForwardConfiguration = new StoreAndForwardConfiguration(timeToLiveSecs);
            return (storeAndForwardEnabled, usePersistentStorage, storeAndForwardConfiguration, storagePath);
        }

        string GetStoragePath()
        {
            string baseStoragePath = this.configuration.GetValue<string>("storageFolder");
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }

            string storagePath = Path.Combine(baseStoragePath, Constants.EdgeHubStorageFolder);
            Directory.CreateDirectory(storagePath);
            return storagePath;
        }

        Option<T> GetConfigurationValueIfExists<T>(string key)
            where T : class
        {
            var value = this.configuration.GetValue<T>(key);
            return EqualityComparer<T>.Default.Equals(value, default(T)) ? Option.None<T>() : Option.Some(value);
        }

        Option<long> GetConfigurationValueIfExists(string key)
        {
            long value = this.configuration.GetValue(key, long.MinValue);
            return value == long.MinValue ? Option.None<long>() : Option.Some(value);
        }
    }
}
