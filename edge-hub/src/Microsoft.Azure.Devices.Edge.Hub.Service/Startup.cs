// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
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
        readonly Client.IotHubConnectionStringBuilder iotHubConnectionStringBuilder;
        readonly string edgeHubConnectionString;

        // ReSharper disable once UnusedParameter.Local
        public Startup(IHostingEnvironment env)
        {
            this.Configuration = new ConfigurationBuilder()
                .AddJsonFile(Constants.ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            this.edgeHubConnectionString = this.Configuration.GetValue<string>("IotHubConnectionString");
            this.iotHubConnectionStringBuilder = Client.IotHubConnectionStringBuilder.Create(this.edgeHubConnectionString);
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

            var topics = new MessageAddressConversionConfiguration(
                this.Configuration.GetSection(Constants.TopicNameConversionSectionName + ":InboundTemplates").Get<List<string>>(),
                this.Configuration.GetSection(Constants.TopicNameConversionSectionName + ":OutboundTemplates").Get<Dictionary<string, string>>());

            string configSource = this.Configuration.GetValue<string>("configSource");
            bool useTwinConfig = !string.IsNullOrWhiteSpace(configSource) && configSource.Equals("twin", StringComparison.OrdinalIgnoreCase);

            var routes = this.Configuration.GetSection("routes").Get<Dictionary<string, string>>();
            (bool isEnabled, bool usePersistentStorage, StoreAndForwardConfiguration config, string storagePath) storeAndForward = this.GetStoreAndForwardConfiguration();

            IConfiguration mqttSettingsConfiguration = this.Configuration.GetSection("appSettings");
            Option<UpstreamProtocol> upstreamProtocolOption = Enum.TryParse(this.Configuration.GetValue("UpstreamProtocol", string.Empty), false, out UpstreamProtocol upstreamProtocol)
                ? Option.Some(upstreamProtocol)
                : Option.None<UpstreamProtocol>();

            string certPath = Path.Combine(
                this.Configuration.GetValue<string>(Constants.SslCertPathEnvName),
                this.Configuration.GetValue<string>(Constants.SslCertEnvName));
            var tlsCertificate = new X509Certificate2(certPath);
            string hostName = this.Configuration.GetValue<string>("HostName");
            IConfiguration amqpSettings = this.Configuration.GetSection("amqp");

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

            // Register modules
            builder.RegisterModule(
                new CommonModule(
                    this.GetProductInfo(),
                    this.iotHubConnectionStringBuilder.HostName,
                    this.iotHubConnectionStringBuilder.DeviceId));
            builder.RegisterModule(
                new RoutingModule(
                    this.iotHubConnectionStringBuilder.HostName,
                    this.iotHubConnectionStringBuilder.DeviceId,
                    this.edgeHubConnectionString,
                    routes,
                    storeAndForward.isEnabled,
                    storeAndForward.usePersistentStorage,
                    storeAndForward.config,
                    storeAndForward.storagePath,
                    connectionPoolSize,
                    useTwinConfig,
                    this.VersionInfo,
                    upstreamProtocolOption));

            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration, topics, tlsCertificate, storeAndForward.isEnabled));
            builder.RegisterModule(new AmqpModule(amqpSettings["scheme"], hostName, amqpSettings.GetValue<ushort>("port"), tlsCertificate, this.iotHubConnectionStringBuilder.HostName));
            builder.RegisterModule(new HttpModule());
            builder.RegisterInstance<IStartup>(this);

            IContainer container = builder.Build();
            return container;
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAuthenticationMiddleware(this.iotHubConnectionStringBuilder.HostName);
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

        string GetProductInfo()
        {
            string name = "Microsoft.Azure.Devices.Edge.Hub";
            string version = FileVersionInfo.GetVersionInfo(typeof(Startup).Assembly.Location).ProductVersion;
            return $"{name}/{version}";
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
    }
}
