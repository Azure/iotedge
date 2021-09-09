// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class MqttBrokerModule : Module
    {
        static readonly int defaultPort = 1882;
        static readonly string defaultUrl = "127.0.0.1";

        readonly IConfiguration config;

        public MqttBrokerModule(IConfiguration config)
        {
            this.config = Preconditions.CheckNotNull(config, nameof(config));
        }

        protected override void Load(ContainerBuilder builder)
        {
            var componentTypes = MqttBridgeComponentDiscovery.GetCandidateTypes().ToArray();

            // The classes will be registered by two types:
            // 1) by its own type - that is because the connector is going to
            //    request an instance by the type name as it finds ISubscriber, etc
            //    derived types.
            // 2) by every interface name that is not the following:
            //      IMessageConsumer/IProducer/ISubscriber/ISubscriptionWatcher.
            //    That is because some of the producers/consumers are also special services
            //    and components will need those injected by custom interfaces.
            builder.RegisterTypes(componentTypes)
                   .AsSelf()
                   .As(t => GetNonStandardBridgeInterfaces(t))
                   .SingleInstance();

            builder.RegisterType<DeviceProxy>()
                   .AsSelf();

            builder.RegisterType<SubscriptionChangeHandler>()
                   .AsImplementedInterfaces()
                   .SingleInstance();

            builder.Register(c => new SystemComponentIdProvider(c.ResolveNamed<IClientCredentials>("EdgeHubCredentials")))
                   .As<ISystemComponentIdProvider>()
                   .SingleInstance();

            builder.Register(c => MqttBridgeComponentDiscovery.Discover(c))
                   .As<IComponentDiscovery>()
                   .SingleInstance();

            builder.RegisterType<MqttBrokerConnector>()
                   .AsImplementedInterfaces()
                   .SingleInstance();

            builder.Register(
                        c =>
                        {
                            var connector = c.Resolve<IMqttBrokerConnector>();

                            var port = this.config.GetValue("port", defaultPort);
                            var baseUrl = this.config.GetValue("url", defaultUrl);

                            var config = new MqttBrokerProtocolHeadConfig(port, baseUrl);

                            return new MqttBrokerProtocolHead(config, connector);
                        })
                    .As<MqttBrokerProtocolHead>()
                    .SingleInstance();

            // The purpose of this setting is to setup a cleanup timer throwing away unanswered message tokens to
            // prevent memory leak. Giving a big enough multiplier to avoid deleting tokens in use, but also
            // not to spin the clean cycle too much, even if the timeout value is short
            var ackTimeout = Math.Max(this.config.GetValue("MessageAckTimeoutSecs", 30), 30);
            builder.RegisterInstance(new ModuleToModuleResponseTimeout(TimeSpan.FromSeconds(ackTimeout * 10)))
                   .SingleInstance();

            base.Load(builder);
        }

        static IEnumerable<Type> GetNonStandardBridgeInterfaces(Type type) => type.GetInterfaces().Where(t => !MqttBridgeComponentDiscovery.CandidateInterfaces.Contains(t));
    }
}
