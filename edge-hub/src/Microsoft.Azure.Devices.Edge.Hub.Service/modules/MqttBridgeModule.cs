// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System;
    using System.Linq;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class MqttBridgeModule : Module
    {
        static readonly int defaultPort = 8000;
        static readonly string defaultUrl = "localhost";

        readonly IConfiguration config;

        public MqttBridgeModule(IConfiguration config)
        {
            this.config = Preconditions.CheckNotNull(config, nameof(config));
        }

        protected override void Load(ContainerBuilder builder)
        {
            var componentTypes = MqttBridgeComponentDiscovery.GetCandidateTypes().ToArray();
            builder.RegisterTypes(componentTypes).AsSelf();

            builder.Register(
                        c =>
                        {
                            var loggerFactory = c.Resolve<ILoggerFactory>();
                            ILogger logger = loggerFactory.CreateLogger(typeof(MqttBridgeComponentDiscovery));

                            var discovery = new MqttBridgeComponentDiscovery(logger);
                            discovery.Discover(c);

                            var connector = new MqttBridgeConnector(discovery);

                            var port = this.config.GetValue("port", defaultPort);
                            var baseUrl = this.config.GetValue("url", defaultUrl);

                            var config = new MqttBridgeProtocolHeadConfig(port, baseUrl);

                            return new MqttBridgeProtocolHead(config, connector);
                        })
                    .As<MqttBridgeProtocolHead>()
                    .SingleInstance();

            base.Load(builder);
        }
    }
}
