// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
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

        public Startup(IHostingEnvironment env)
        {
            this.Configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigFileName)
                .AddEnvironmentVariables()
                .Build();
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
            string iothubHostname = this.GetIotHubHostName();
            string edgeDeviceId = this.Configuration.GetValue<string>("EdgeDeviceId");

            var topics = new MessageAddressConversionConfiguration(
                this.Configuration.GetSection(TopicNameConversionSectionName + ":InboundTemplates").Get<List<string>>(),
                this.Configuration.GetSection(TopicNameConversionSectionName + ":OutboundTemplates").Get<Dictionary<string, string>>());
            var routes = this.Configuration.GetSection("routes").Get<List<string>>();

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

            builder.RegisterModule(new CommonModule(iothubHostname, edgeDeviceId));            
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration, topics));
            builder.RegisterModule(new RoutingModule(iothubHostname, edgeDeviceId, routes));
            builder.RegisterModule(new HttpModule());
            builder.RegisterInstance<IStartup>(this);
                        
            IContainer container = builder.Build();            
            return container;
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAuthenticationMiddleware(this.GetIotHubHostName());            
            app.UseMvc();           
        }

        string GetIotHubHostName() => this.Configuration.GetValue<string>("IotHubHostName");        
    }
}