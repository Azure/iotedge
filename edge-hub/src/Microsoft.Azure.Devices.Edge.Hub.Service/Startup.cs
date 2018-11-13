// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System;
    using System.Collections.Generic;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Swagger;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Swashbuckle.AspNetCore.Swagger;

    public class Startup : IStartup
    {
        const string MethodsApiTitle = "Method Invoke API";
        const string MethodsApiDescription = "API for methods invoke on devices and on modules.";
        readonly IDependencyManager dependencyManager;
        readonly IConfigurationRoot configuration;

        // ReSharper disable once UnusedParameter.Local
        public Startup(
            IHostingEnvironment env,
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
            services.AddMvc(options => options.Filters.Add(typeof(ExceptionFilter)));

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.TagActionsBy(apiDesc => new List<string> { apiDesc.HttpMethod.ToString() });
                c.SwaggerDoc("methods", new Info
                {
                    Version = HttpConstants.ApiVersion,
                    Title = MethodsApiTitle,
                    Description = MethodsApiDescription
                });
                c.DocumentFilter<SwaggerDocumentFilter>();
                c.OperationFilter<SwaggerOperationFilter>();
            });

            services.Configure<MvcOptions>(options => { options.Filters.Add(new RequireHttpsAttribute()); });
            this.Container = this.BuildContainer(services);

            return new AutofacServiceProvider(this.Container);
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

            app.Use(
                async (context, next) =>
                {
                    // Response header is added to prevent MIME type sniffing
                    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                    await next();
                });
            app.UseSwagger(
                   c =>
                   {
                       c.RouteTemplate = "swagger/{documentName}/swagger.json";
                       c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                       {
                           swaggerDoc.Host = "hostname";
                       });
                   })
               .UseSwaggerUI(
                   c =>
                   {
                       c.SwaggerEndpoint("/swagger/methods/swagger.json", MethodsApiDescription);
                   });

            app.UseAuthenticationMiddleware(iotHubHostname, edgeDeviceId);
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
