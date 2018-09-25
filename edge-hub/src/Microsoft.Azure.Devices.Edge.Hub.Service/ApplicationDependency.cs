namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
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
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;

    class ApplicationDependency : IApplicationDependency
    {
        readonly IConfigurationRoot configuration;
        readonly X509Certificate2 serverCertificate;

        readonly string iotHubHostname;
        readonly string edgeDeviceId;
        readonly string edgeModuleId;
        readonly string edgeDeviceHostName;
        readonly Option<string> connectionString;
        readonly VersionInfo versionInfo;

        public ApplicationDependency(IConfigurationRoot configuration, X509Certificate2 serverCertificate)
        {
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.serverCertificate = Preconditions.CheckNotNull(serverCertificate, nameof(serverCertificate));

            string edgeHubConnectionString = this.configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);
            if (!string.IsNullOrWhiteSpace(edgeHubConnectionString))
            {
                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
                this.iotHubHostname = iotHubConnectionStringBuilder.HostName;
                this.edgeDeviceId = iotHubConnectionStringBuilder.DeviceId;
                this.edgeModuleId = iotHubConnectionStringBuilder.ModuleId;
                this.edgeDeviceHostName = string.Empty;
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
        
        public void RegisterTo(ContainerBuilder builder)
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

            // TODO: should it be moved to Edge hub program.cs and call just before calling EdgeHubProtocol StartAsync method?
            Metrics.BuildMetricsCollector(this.configuration);

            bool optimizeForPerformance = this.configuration.GetValue("OptimizeForPerformance", true);
            (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward = this.GetStoreAndForwardConfiguration();

            this.RegisterCommonModule(builder, optimizeForPerformance, storeAndForward);
            this.RegisterRoutingModule(builder, storeAndForward);
            this.RegisterMqttModule(builder, storeAndForward, optimizeForPerformance);
            this.RegisterAmqpModule(builder);
            builder.RegisterModule(new HttpModule());
        }

        void RegisterAmqpModule(ContainerBuilder builder)
        {
            IConfiguration amqpSettings = this.configuration.GetSection("amqpSettings");
            builder.RegisterModule(new AmqpModule(amqpSettings["scheme"], amqpSettings.GetValue<ushort>("port"), this.serverCertificate, this.iotHubHostname));
        }

        void RegisterMqttModule(ContainerBuilder builder, (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward, bool optimizeForPerformance)
        {
            var topics = new MessageAddressConversionConfiguration(
                this.configuration.GetSection(Constants.TopicNameConversionSectionName + ":InboundTemplates").Get<List<string>>(),
                this.configuration.GetSection(Constants.TopicNameConversionSectionName + ":OutboundTemplates").Get<Dictionary<string, string>>());
            string caChainPath = this.configuration.GetValue(Constants.ConfigKey.EdgeHubServerCAChainCertificateFile, string.Empty);

            // TODO: We don't want to make enabling Cert Auth configurable right now. Turn off Cert auth. 
            //bool clientCertAuthEnabled = this.Configuration.GetValue("ClientCertAuthEnabled", false);
            bool clientCertAuthEnabled = false;

            IConfiguration mqttSettingsConfiguration = this.configuration.GetSection("mqttSettings");
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration, topics, this.serverCertificate, storeAndForward.isEnabled, clientCertAuthEnabled, caChainPath, optimizeForPerformance));
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
                    closeCloudConnectionOnIdleTimeout));
        }

        void RegisterCommonModule(ContainerBuilder builder, bool optimizeForPerformance, (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward)
        {
            string productInfo = VersionInfo.Get(Constants.VersionInfoFileName).ToString();
            bool cacheTokens = this.configuration.GetValue("CacheTokens", false);
            Option<string> workloadUri = this.GetConfigurationValueIfExists<string>(Constants.ConfigKey.WorkloadUri);
            Option<string> moduleGenerationId = this.GetConfigurationValueIfExists<string>(Constants.ConfigKey.ModuleGenerationId);

            if (!Enum.TryParse(this.configuration.GetValue("AuthenticationMode", string.Empty), true, out AuthenticationMode authenticationMode))
            {
                authenticationMode = AuthenticationMode.CloudAndScope;
            }

            int scopeCacheRefreshRateSecs = this.configuration.GetValue("DeviceScopeCacheRefreshRateSecs", 3600);
            TimeSpan scopeCacheRefreshRate = TimeSpan.FromSeconds(scopeCacheRefreshRateSecs);

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
                    scopeCacheRefreshRate,
                    cacheTokens));
        }

        internal static Option<UpstreamProtocol> GetUpstreamProtocol(IConfigurationRoot configuration) =>
            Enum.TryParse(configuration.GetValue("UpstreamProtocol", string.Empty), true, out UpstreamProtocol upstreamProtocol)
                ? Option.Some(upstreamProtocol)
                : Option.None<UpstreamProtocol>();
        
        (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) GetStoreAndForwardConfiguration()
        {
            int defaultTtl = -1;
            bool isEnabled = this.configuration.GetValue<bool>("storeAndForwardEnabled");
            bool usePersistentStorage = this.configuration.GetValue<bool>("usePersistentStorage");
            int timeToLiveSecs = defaultTtl;
            string storagePath = string.Empty;
            if (isEnabled)
            {
                IConfiguration storeAndForwardConfigurationSection = this.configuration.GetSection("storeAndForward");
                timeToLiveSecs = storeAndForwardConfigurationSection.GetValue("timeToLiveSecs", defaultTtl);

                if (usePersistentStorage)
                {
                    storagePath = this.GetStoragePath();
                }
            }

            var storeAndForwardConfiguration = new StoreAndForwardConfiguration(timeToLiveSecs);
            return (isEnabled, usePersistentStorage, storeAndForwardConfiguration, storagePath);
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
    }
}
