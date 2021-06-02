// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Threading.Tasks;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
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
            app.UseWebSockets();

            var webSocketListenerRegistry = app.ApplicationServices.GetService(typeof(IWebSocketListenerRegistry)) as IWebSocketListenerRegistry;
            var httpProxiedCertificateExtractor = app.ApplicationServices.GetService(typeof(Task<IHttpProxiedCertificateExtractor>)) as Task<IHttpProxiedCertificateExtractor>;
            app.UseWebSocketHandlingMiddleware(webSocketListenerRegistry, httpProxiedCertificateExtractor);

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
