// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Security.Cryptography.X509Certificates;
    using Autofac;
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Hub.Service;
    using Microsoft.Azure.Devices.Edge.Hub.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Logging;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Service.Constants;

    class DependencyManager : IDependencyManager
    {
        readonly IConfigurationRoot configuration;
        readonly X509Certificate2 serverCertificate;
        readonly IList<X509Certificate2> trustBundle;

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

        public DependencyManager(IConfigurationRoot configuration, X509Certificate2 serverCertificate, IList<X509Certificate2> trustBundle)
        {
            this.configuration = configuration;
            this.serverCertificate = serverCertificate;
            this.trustBundle = trustBundle;
        }

        public void Register(ContainerBuilder builder)
        {
            const int ConnectionPoolSize = 10;

            string edgeHubConnectionString = $"{this.configuration[Constants.ConfigKey.IotHubConnectionString]};ModuleId=$edgeHub";
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(edgeHubConnectionString);
            var topics = new MessageAddressConversionConfiguration(this.inboundTemplates, this.outboundTemplates);

            builder.RegisterModule(new LoggingModule());

            var mqttSettingsConfiguration = new Mock<IConfiguration>();
            mqttSettingsConfiguration.Setup(c => c.GetSection(It.IsAny<string>())).Returns(Mock.Of<IConfigurationSection>(s => s.Value == null));

            var experimentalFeatures = new ExperimentalFeatures(true, false, false, true);

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
            var metricsConfig = new MetricsConfig(true, new MetricsListenerConfig());

            builder.RegisterModule(
                new CommonModule(
                    string.Empty,
                    iotHubConnectionStringBuilder.HostName,
                    iotHubConnectionStringBuilder.DeviceId,
                    iotHubConnectionStringBuilder.ModuleId,
                    string.Empty,
                    Option.None<string>(),
                    AuthenticationMode.CloudAndScope,
                    Option.Some(edgeHubConnectionString),
                    false,
                    false,
                    string.Empty,
                    Option.None<string>(),
                    Option.None<string>(),
                    TimeSpan.FromHours(1),
                    false,
                    this.trustBundle,
                    string.Empty,
                    metricsConfig,
                    false,
                    string.Empty));

            builder.RegisterModule(
                new RoutingModule(
                    iotHubConnectionStringBuilder.HostName,
                    iotHubConnectionStringBuilder.DeviceId,
                    iotHubConnectionStringBuilder.ModuleId,
                    Option.Some(edgeHubConnectionString),
                    this.routes,
                    true,
                    storeAndForwardConfiguration,
                    ConnectionPoolSize,
                    false,
                    versionInfo,
                    Option.Some(UpstreamProtocol.Amqp),
                    TimeSpan.FromSeconds(5),
                    101,
                    TimeSpan.FromSeconds(3600),
                    true,
                    TimeSpan.FromSeconds(20),
                    Option.None<TimeSpan>(),
                    Option.None<TimeSpan>(),
                    false,
                    10,
                    10,
                    false,
                    TimeSpan.FromHours(1),
                    experimentalFeatures));

            builder.RegisterModule(new HttpModule());
            builder.RegisterModule(new MqttModule(mqttSettingsConfiguration.Object, topics, this.serverCertificate, false, false, false));
            builder.RegisterModule(new AmqpModule("amqps", 5671, this.serverCertificate, iotHubConnectionStringBuilder.HostName, true));
        }
    }
}
