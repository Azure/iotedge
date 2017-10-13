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
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class Startup : IStartup
    {
        const string ConfigFileName = "appsettings_hub.json";
        const string TopicNameConversionSectionName = "mqttTopicNameConversion";
        readonly Client.IotHubConnectionStringBuilder iotHubConnectionStringBuilder;
        readonly string edgeHubConnectionString;

        public Startup(IHostingEnvironment env)
        {
            this.Configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            this.edgeHubConnectionString = this.Configuration.GetValue<string>("IotHubConnectionString");
            this.iotHubConnectionStringBuilder = Client.IotHubConnectionStringBuilder.Create(this.edgeHubConnectionString);
        }

        public IConfigurationRoot Configuration { get; }

        internal IContainer Container { get; private set; }

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
                this.Configuration.GetSection(TopicNameConversionSectionName + ":InboundTemplates").Get<List<string>>(),
                this.Configuration.GetSection(TopicNameConversionSectionName + ":OutboundTemplates").Get<Dictionary<string, string>>());

            string configSource = this.Configuration.GetValue<string>("configSource");
            bool useTwinConfig = !string.IsNullOrWhiteSpace(configSource) && configSource.Equals("twin", StringComparison.OrdinalIgnoreCase);

            var routes = this.Configuration.GetSection("routes").Get<Dictionary<string, string>>();
            (bool isEnabled, StoreAndForwardConfiguration config) storeAndForward = this.GetStoreAndForwardConfiguration();

            IConfiguration mqttSettingsConfiguration = this.Configuration.GetSection("appSettings");                        

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
            builder.RegisterModule(new CommonModule(this.iotHubConnectionStringBuilder.HostName, this.iotHubConnectionStringBuilder.DeviceId));            
            builder.RegisterModule(
                new RoutingModule(
                    this.iotHubConnectionStringBuilder.HostName, 
                    this.iotHubConnectionStringBuilder.DeviceId, 
                    this.edgeHubConnectionString,
                    routes, 
                    storeAndForward.isEnabled, 
                    storeAndForward.config, 
                    connectionPoolSize,
                    useTwinConfig));
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration, topics, storeAndForward.isEnabled));
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

        (bool isEnabled, StoreAndForwardConfiguration config) GetStoreAndForwardConfiguration()
        {
            int defaultTtl = -1;
            bool isEnabled = this.Configuration.GetValue<bool>("storeAndForwardEnabled");
            int timeToLiveSecs = defaultTtl;
            if(isEnabled)
            {
                IConfiguration storeAndForwardConfigurationSection = this.Configuration.GetSection("storeAndForward");
                timeToLiveSecs = storeAndForwardConfigurationSection.GetValue<int>("timeToLiveSecs", defaultTtl);
            }
            var storeAndForwardConfiguration = new StoreAndForwardConfiguration(timeToLiveSecs);
            return (isEnabled, storeAndForwardConfiguration);            
        }
    }
}