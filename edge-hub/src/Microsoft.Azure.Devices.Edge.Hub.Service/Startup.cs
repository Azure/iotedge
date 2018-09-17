// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.IO;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class Startup : IStartup
    {
        readonly string iotHubHostname;
        readonly string edgeDeviceId;
        readonly string edgeModuleId;
        readonly string edgeDeviceHostName;
        readonly Option<string> connectionString;

        // ReSharper disable once UnusedParameter.Local
        public Startup(IHostingEnvironment env)
        {
            this.Configuration = new ConfigurationBuilder()
                .AddJsonFile(Constants.ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            string edgeHubConnectionString = this.Configuration.GetValue<string>(Constants.IotHubConnectionStringVariableName);
            if (!string.IsNullOrWhiteSpace(edgeHubConnectionString))
            {
                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = Client.IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
                this.iotHubHostname = iotHubConnectionStringBuilder.HostName;
                this.edgeDeviceId = iotHubConnectionStringBuilder.DeviceId;
                this.edgeModuleId = iotHubConnectionStringBuilder.ModuleId;
                this.edgeDeviceHostName = string.Empty;
                this.connectionString = Option.Some(edgeHubConnectionString);
            }
            else
            {
                this.iotHubHostname = this.Configuration.GetValue<string>(Constants.IotHubHostnameVariableName);
                this.edgeDeviceId = this.Configuration.GetValue<string>(Constants.DeviceIdVariableName);
                this.edgeModuleId = this.Configuration.GetValue<string>(Constants.ModuleIdVariableName);
                this.edgeDeviceHostName = this.Configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
                this.connectionString = Option.None<string>();
            }

            this.VersionInfo = VersionInfo.Get(Constants.VersionInfoFileName);
        }

        public IConfigurationRoot Configuration { get; }

        internal IContainer Container { get; private set; }

        public VersionInfo VersionInfo { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddMvc(options => options.Filters.Add(typeof(ExceptionFilter)));

            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new RequireHttpsAttribute());
            });

            services.AddSingleton<IStartup>(sp => this);
            this.Container = this.BuildContainer(services);

            return new AutofacServiceProvider(this.Container);
        }

        IContainer BuildContainer(IServiceCollection services)
        {
            int connectionPoolSize = this.Configuration.GetValue<int>("IotHubConnectionPoolSize");
            bool optimizeForPerformance = this.Configuration.GetValue("OptimizeForPerformance", true);
            var topics = new MessageAddressConversionConfiguration(
                this.Configuration.GetSection(Constants.TopicNameConversionSectionName + ":InboundTemplates").Get<List<string>>(),
                this.Configuration.GetSection(Constants.TopicNameConversionSectionName + ":OutboundTemplates").Get<Dictionary<string, string>>());

            string configSource = this.Configuration.GetValue<string>("configSource");
            bool useTwinConfig = !string.IsNullOrWhiteSpace(configSource) && configSource.Equals("twin", StringComparison.OrdinalIgnoreCase);

            var routes = this.Configuration.GetSection("routes").Get<Dictionary<string, string>>();
            (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward = this.GetStoreAndForwardConfiguration();

            IConfiguration mqttSettingsConfiguration = this.Configuration.GetSection("mqttSettings");
            Option<UpstreamProtocol> upstreamProtocolOption = GetUpstreamProtocol(this.Configuration);
            int connectivityCheckFrequencySecs = this.Configuration.GetValue("ConnectivityCheckFrequencySecs", 300);
            TimeSpan connectivityCheckFrequency = connectivityCheckFrequencySecs < 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(connectivityCheckFrequencySecs);

            // TODO: We don't want to make enabling Cert Auth configurable right now. Turn off Cert auth. 
            //bool clientCertAuthEnabled = this.Configuration.GetValue("ClientCertAuthEnabled", false);
            bool clientCertAuthEnabled = false;
            bool cacheTokens = this.Configuration.GetValue("CacheTokens", false);
            Option<string> workloadUri = this.GetConfigurationValueIfExists<string>(Constants.WorkloadUriVariableName);
            Option<string> moduleGenerationId = this.GetConfigurationValueIfExists<string>(Constants.ModuleGenerationIdVariableName);

            string caChainPath = this.Configuration.GetValue("EdgeModuleHubServerCAChainCertificateFile", string.Empty);
            // n Clients + 1 Edgehub
            int maxConnectedClients = this.Configuration.GetValue("MaxConnectedClients", 100) + 1;

            IConfiguration amqpSettings = this.Configuration.GetSection("amqpSettings");

            if (!Enum.TryParse(this.Configuration.GetValue("AuthenticationMode", string.Empty), true, out AuthenticationMode authenticationMode))
            {
                authenticationMode = AuthenticationMode.CloudAndScope;
            }

            int scopeCacheRefreshRateSecs = this.Configuration.GetValue("DeviceScopeCacheRefreshRateSecs", 3600);
            TimeSpan scopeCacheRefreshRate = TimeSpan.FromSeconds(scopeCacheRefreshRateSecs);

            int cloudConnectionIdleTimeoutSecs = this.Configuration.GetValue("CloudConnectionIdleTimeoutSecs", 3600);
            TimeSpan cloudConnectionIdleTimeout = TimeSpan.FromHours(cloudConnectionIdleTimeoutSecs);
            bool closeCloudConnectionOnIdleTimeout = this.Configuration.GetValue("CloseCloudConnectionOnIdleTimeout", true);

            var builder = new ContainerBuilder();
            builder.Populate(services);

            builder.RegisterModule(new LoggingModule());
            builder.RegisterBuildCallback(
                c =>
                {
                    // set up loggers for dotnetty
                    var loggerFactory = c.Resolve<ILoggerFactory>();
                    InternalLoggerFactory.DefaultFactory = loggerFactory;

                    var eventListener = new LoggerEventListener(loggerFactory.CreateLogger("ProtocolGateway"));
                    eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Informational);
                });

            string productInfo = VersionInfo.Get(Constants.VersionInfoFileName).ToString();

            Metrics.BuildMetricsCollector(this.Configuration);

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
                    this.VersionInfo,
                    upstreamProtocolOption,
                    connectivityCheckFrequency,
                    maxConnectedClients,
                    cloudConnectionIdleTimeout,
                    closeCloudConnectionOnIdleTimeout));

            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration, topics, ServerCertificateCache.X509Certificate, storeAndForward.isEnabled, clientCertAuthEnabled, caChainPath, optimizeForPerformance));
            builder.RegisterModule(new AmqpModule(amqpSettings["scheme"], amqpSettings.GetValue<ushort>("port"), ServerCertificateCache.X509Certificate, this.iotHubHostname));
            builder.RegisterModule(new HttpModule());
            builder.RegisterInstance<IStartup>(this);

            IContainer container = builder.Build();
            return container;
        }

        internal static Option<UpstreamProtocol> GetUpstreamProtocol(IConfigurationRoot configuration) =>
            Enum.TryParse(configuration.GetValue("UpstreamProtocol", string.Empty), true, out UpstreamProtocol upstreamProtocol)
                ? Option.Some(upstreamProtocol)
                : Option.None<UpstreamProtocol>();

        public void Configure(IApplicationBuilder app)
        {
            var webSocketListenerRegistry = app.ApplicationServices.GetService(typeof(IWebSocketListenerRegistry)) as IWebSocketListenerRegistry;

            app.UseWebSockets();
            app.UseWebSocketHandlingMiddleware(webSocketListenerRegistry);
            app.UseAuthenticationMiddleware(this.iotHubHostname);
            app.UseMvc();
        }

        (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) GetStoreAndForwardConfiguration()
        {
            int defaultTtl = -1;
            bool isEnabled = this.Configuration.GetValue<bool>("storeAndForwardEnabled");
            bool usePersistentStorage = this.Configuration.GetValue<bool>("usePersistentStorage");
            int timeToLiveSecs = defaultTtl;
            string storagePath = string.Empty;
            if (isEnabled)
            {
                IConfiguration storeAndForwardConfigurationSection = this.Configuration.GetSection("storeAndForward");
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
            string baseStoragePath = this.Configuration.GetValue<string>("storageFolder");
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }
            string storagePath = Path.Combine(baseStoragePath, Constants.EdgeHubStorageFolder);
            Directory.CreateDirectory(storagePath);
            return storagePath;
        }

        Option<T> GetConfigurationValueIfExists<T>(string key) where T : class
        {
            var value = this.Configuration.GetValue<T>(key);
            return EqualityComparer<T>.Default.Equals(value, default(T)) ? Option.None<T>() : Option.Some(value);
        }
    }
}
