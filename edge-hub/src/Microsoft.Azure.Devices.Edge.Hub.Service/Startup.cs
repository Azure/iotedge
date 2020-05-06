// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class Startup : IStartup
    {
        readonly IDependencyManager dependencyManager;
        readonly IConfigurationRoot configuration;
        readonly CertChainMapper certChainMapper;

        // ReSharper disable once UnusedParameter.Local
        public Startup(
            IConfigurationRoot configuration,
            IDependencyManager dependencyManager,
            CertChainMapper certChainMapper)
        {
            this.configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.dependencyManager = Preconditions.CheckNotNull(dependencyManager, nameof(dependencyManager));
            this.certChainMapper = Preconditions.CheckNotNull(certChainMapper, nameof(certChainMapper));
        }

        internal IContainer Container { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddMvc(options => options.Filters.Add(typeof(ExceptionFilter))).AddNewtonsoftJson();
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new RequireHttpsAttribute());
                options.EnableEndpointRouting = false;
            });
            this.Container = this.BuildContainer(services);

            return new AutofacServiceProvider(this.Container);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseWebSockets();

            app.Use(
                async (context, next) =>
                {
                    Option<IList<X509Certificate2>> certChainOption = this.certChainMapper.ExtractCertChain(context.Connection);
                    certChainOption.ForEach(certChain =>
                    {
                        TlsConnectionFeatureExtended tlsConnectionFeatureExtended = new TlsConnectionFeatureExtended
                        {
                            ChainElements = certChain
                        };
                        context.Features.Set<ITlsConnectionFeatureExtended>(tlsConnectionFeatureExtended);
                    });
                    await next();
                });

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

            app.Use(
                async (context, next) =>
                {
                    // Response header is added to prevent MIME type sniffing
                    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                    await next();
                });

            app.UseMvc();
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
