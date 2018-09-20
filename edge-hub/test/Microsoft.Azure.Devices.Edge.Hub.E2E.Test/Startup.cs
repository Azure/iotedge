// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
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
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Middleware;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;

    class Startup : IStartup
    {
        internal IContainer Container { get; private set; }

        string iothubHostname;
        string deviceId;

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
            const int ConnectionPoolSize = 10;

            string edgeHubConnectionString = $"{ProtocolHeadFixtureCache.EdgeDeviceConnectionString};ModuleId=$edgeHub";
            Client.IotHubConnectionStringBuilder iotHubConnectionStringBuilder = Client.IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
            this.iothubHostname = iotHubConnectionStringBuilder.HostName;
            this.deviceId = iotHubConnectionStringBuilder.DeviceId;
            var topics = new MessageAddressConversionConfiguration(this.inboundTemplates, this.outboundTemplates);

            var builder = new ContainerBuilder();
            builder.Populate(services);
            builder.RegisterModule(new LoggingModule());

            var mqttSettingsConfiguration = new Mock<IConfiguration>();
            mqttSettingsConfiguration.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>(s => s.Value == null));

            builder.RegisterBuildCallback(
                c =>
                {
                    // set up loggers for dotnetty
                    var loggerFactory = c.Resolve<ILoggerFactory>();
                    InternalLoggerFactory.DefaultFactory = loggerFactory;

                    var eventListener = new LoggerEventListener(loggerFactory.CreateLogger("ProtocolGateway"));
                    eventListener.EnableEvents(CommonEventSource.Log, EventLevel.Informational);
                });

            var versionInfo = new VersionInfo("v1", "b1", "c1");
            var storeAndForwardConfiguration = new StoreAndForwardConfiguration(-1);
            builder.RegisterModule(
                new CommonModule(
                    string.Empty,
                    iotHubConnectionStringBuilder.HostName,
                    iotHubConnectionStringBuilder.DeviceId,
                    iotHubConnectionStringBuilder.ModuleId,
                    string.Empty,
                    Option.None<string>(),
                    AuthenticationMode.CloudAndScope,
                    Option.Some(ProtocolHeadFixtureCache.EdgeDeviceConnectionString),
                    false,
                    false,
                    string.Empty,
                    Option.None<string>(),
                    TimeSpan.FromHours(1),
                    false));

            builder.RegisterModule(
                new RoutingModule(
                    iotHubConnectionStringBuilder.HostName,
                    iotHubConnectionStringBuilder.DeviceId,
                    iotHubConnectionStringBuilder.ModuleId,
                    Option.Some(edgeHubConnectionString),
                    this.routes,
                    false,
                    storeAndForwardConfiguration,
                    ConnectionPoolSize,
                    false,
                    versionInfo,
                    Option.Some(UpstreamProtocol.Amqp),
                    TimeSpan.FromSeconds(5),
                    101,
                    TimeSpan.FromSeconds(3600),
                    true));

            builder.RegisterModule(new HttpModule());
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration.Object, topics, ProtocolHeadFixtureCache.X509Certificate, false, false, string.Empty, false));
            builder.RegisterModule(new AmqpModule("amqps", 5671, ProtocolHeadFixtureCache.X509Certificate, iotHubConnectionStringBuilder.HostName));
            return builder.Build();
        }

        public void Configure(IApplicationBuilder app)
        {
            var webSocketListenerRegistry = app.ApplicationServices.GetService(typeof(IWebSocketListenerRegistry)) as IWebSocketListenerRegistry;

            app.UseWebSockets();
            app.UseWebSocketHandlingMiddleware(webSocketListenerRegistry);
            app.UseAuthenticationMiddleware(this.iothubHostname, this.deviceId);
            app.UseMvc();
        }

        readonly IList<string> inboundTemplates = new List<string>()
            {
                "devices/{deviceId}/messages/events/{params}/",
                "devices/{deviceId}/messages/events/",
                "devices/{deviceId}/modules/{moduleId}/messages/events/{params}/",
                "devices/{deviceId}/modules/{moduleId}/messages/events/",
                "$iothub/methods/res/{statusCode}/?$rid={correlationId}",
                "$iothub/methods/res/{statusCode}/?$rid={correlationId}&foo={bar}"
            };

        readonly IDictionary<string, string> outboundTemplates = new Dictionary<string, string>()
            {
                { "C2D", "devices/{deviceId}/messages/devicebound" },
                { "TwinEndpoint", "$iothub/twin/res/{statusCode}/?$rid={correlationId}" },
                { "TwinDesiredPropertyUpdate", "$iothub/twin/PATCH/properties/desired/?$version={version}" },
                { "ModuleEndpoint", "devices/{deviceId}/modules/{moduleId}/inputs/{inputName}" }
            };

        readonly IDictionary<string, string> routes = new Dictionary<string, string>()
        {
            ["r1"] = "FROM /messages/events INTO $upstream",
            ["r2"] = "FROM /messages/modules/senderA INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            ["r3"] = "FROM /messages/modules/senderB INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            ["r4"] = "FROM /messages/modules/sender1 INTO BrokeredEndpoint(\"/modules/receiver1/inputs/input1\")",
            ["r5"] = "FROM /messages/modules/sender2 INTO BrokeredEndpoint(\"/modules/receiver2/inputs/input1\")",
            ["r6"] = "FROM /messages/modules/sender3 INTO BrokeredEndpoint(\"/modules/receiver3/inputs/input1\")",
            ["r7"] = "FROM /messages/modules/sender4 INTO BrokeredEndpoint(\"/modules/receiver4/inputs/input1\")",
            ["r8"] = "FROM /messages/modules/sender5 INTO BrokeredEndpoint(\"/modules/receiver5/inputs/input1\")",
            ["r9"] = "FROM /messages/modules/sender6 INTO BrokeredEndpoint(\"/modules/receiver6/inputs/input1\")",
            ["r10"] = "FROM /messages/modules/sender7 INTO BrokeredEndpoint(\"/modules/receiver7/inputs/input1\")",
            ["r11"] = "FROM /messages/modules/sender8 INTO BrokeredEndpoint(\"/modules/receiver8/inputs/input1\")",
            ["r12"] = "FROM /messages/modules/sender9 INTO BrokeredEndpoint(\"/modules/receiver9/inputs/input1\")",
            ["r13"] = "FROM /messages/modules/sender10 INTO BrokeredEndpoint(\"/modules/receiver10/inputs/input1\")",
            ["r14"] = "FROM /messages/modules/sender11/outputs/output1 INTO BrokeredEndpoint(\"/modules/receiver11/inputs/input1\")",
            ["r15"] = "FROM /messages/modules/sender11/outputs/output2 INTO BrokeredEndpoint(\"/modules/receiver11/inputs/input2\")",
        };
    }
}
