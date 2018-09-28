// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class Startup : IStartup
    {
        readonly IApplicationDependency applicationDependency;
        readonly IConfigurationRoot configuration;

        // ReSharper disable once UnusedParameter.Local
        public Startup(
            IHostingEnvironment env,
            IConfigurationRoot configuration,
            IApplicationDependency applicationDependency)
        {
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.applicationDependency = Preconditions.CheckNotNull(applicationDependency, nameof(applicationDependency));
        }

        internal IContainer Container { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddMvc(options => options.Filters.Add(typeof(ExceptionFilter)));
            services.Configure<MvcOptions>(options => { options.Filters.Add(new RequireHttpsAttribute()); });
            this.Container = this.BuildContainer(services);
            
            return new AutofacServiceProvider(this.Container);
        }

        IContainer BuildContainer(IServiceCollection services)
        {
            var builder = new ContainerBuilder();
            builder.Populate(services);
            this.applicationDependency.RegisterTo(builder);
            builder.RegisterInstance<IStartup>(this);

            return builder.Build();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseWebSockets();

            var webSocketListenerRegistry = app.ApplicationServices.GetService(typeof(IWebSocketListenerRegistry)) as IWebSocketListenerRegistry;
            app.UseWebSocketHandlingMiddleware(webSocketListenerRegistry);

            string edgeHubConnectionString = this.configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);
            string iotHubHostname;
            string edgeDeviceId;
            if (!string.IsNullOrWhiteSpace(edgeHubConnectionString))
            {
                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
                iotHubHostname = iotHubConnectionStringBuilder.HostName;
                edgeDeviceId = iotHubConnectionStringBuilder.DeviceId;
            }
            else
            {
                iotHubHostname = this.configuration.GetValue<string>(Constants.ConfigKey.IotHubHostname);
                edgeDeviceId = this.configuration.GetValue<string>(Constants.ConfigKey.DeviceId);
            }

            app.UseAuthenticationMiddleware(iotHubHostname, edgeDeviceId);

            app.UseMvc();
        }
    }
}
