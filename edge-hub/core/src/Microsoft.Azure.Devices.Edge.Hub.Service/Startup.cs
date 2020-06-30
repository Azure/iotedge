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
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Prometheus;

    public class Startup : IStartup
    {
        readonly IDependencyManager dependencyManager;
        readonly IConfigurationRoot configuration;

        // ReSharper disable once UnusedParameter.Local
        public Startup(
            IConfigurationRoot configuration,
            IDependencyManager dependencyManager)
        {
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.dependencyManager = Preconditions.CheckNotNull(dependencyManager, nameof(dependencyManager));
        }

        internal IContainer Container { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddControllers().AddNewtonsoftJson();
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new RequireHttpsAttribute());
            });

            this.Container = this.BuildContainer(services);

            return new AutofacServiceProvider(this.Container);
        }

        public void Configure(IApplicationBuilder app)
        {
            // Separate metrics endpoint from https authentication
            app.Map("/metrics", metricsApp =>
            {
                metricsApp.UseMetricServer(string.Empty);
            });

            app.UseHttpsRedirection();

            (string iotHubHostname, string edgeDeviceId, string gatewayHostname) = this.GetStartupParameters();
            app.UseMiddleware<RegistryProxyMiddleware>(gatewayHostname);
            app.UseWebSockets();

            var webSocketListenerRegistry = app.ApplicationServices.GetService(typeof(IWebSocketListenerRegistry)) as IWebSocketListenerRegistry;
            app.UseWebSocketHandlingMiddleware(webSocketListenerRegistry);

            app.UseAuthenticationMiddleware(iotHubHostname, edgeDeviceId);

            app.Use(
                async (context, next) =>
                {
                    // Response header is added to prevent MIME type sniffing
                    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                    await next();
                });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        (string iotHubHostname, string edgeDeviceId, string gatewayHostname) GetStartupParameters()
        {
            string iotHubHostname, edgeDeviceId;

            string edgeHubConnectionString = this.configuration.GetValue<string>(Constants.ConfigKey.IotHubConnectionString);
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

            string gatewayHostname = this.configuration.GetValue<string>(Constants.ConfigKey.GatewayHostname) ?? iotHubHostname;

            return (iotHubHostname, edgeDeviceId, gatewayHostname);
        }

        IContainer BuildContainer(IServiceCollection services)
        {
            var builder = new ContainerBuilder();
            builder.Populate(services);
            this.dependencyManager.Register(builder);
            builder.RegisterInstance<IStartup>(this);

            return builder.Build();
        }
    }
}
