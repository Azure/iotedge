// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using Message = Microsoft.Azure.Devices.Client.Message;

    public class RoutingModule : Module
    {
        readonly string iotHubName;
        readonly string edgeDeviceId;
        readonly IEnumerable<string> routes;

        public RoutingModule(string iotHubName, string edgeDeviceId, IEnumerable<string> routes)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.routes = Preconditions.CheckNotNull(routes, nameof(routes));
        }

        protected override void Load(ContainerBuilder builder)
        {
            // IMessageConverter<IRoutingMessage>
            builder.Register(c => new RoutingMessageConverter())
                .As<Core.IMessageConverter<IRoutingMessage>>()
                .SingleInstance();

            // IRoutingPerfCounter
            builder.Register(
                c =>
                {
                    Routing.PerfCounter = NullRoutingPerfCounter.Instance;
                    return Routing.PerfCounter;
                })
                .As<IRoutingPerfCounter>()
                .AutoActivate()
                .SingleInstance();

            // IRoutingUserAnalyticsLogger
            builder.Register(
                c =>
                {
                    Routing.UserAnalyticsLogger = NullUserAnalyticsLogger.Instance;
                    return Routing.UserAnalyticsLogger;
                })
                .As<IRoutingUserAnalyticsLogger>()
                .AutoActivate()
                .SingleInstance();

            // IRoutingUserMetricLogger
            builder.Register(
                c =>
                {
                    Routing.UserMetricLogger = NullRoutingUserMetricLogger.Instance;
                    return Routing.UserMetricLogger;
                })
                .As<IRoutingUserMetricLogger>()
                .AutoActivate()
                .SingleInstance();

            // IMessageConverter<Message>
            builder.Register(c => new MqttMessageConverter())
                .As<Core.IMessageConverter<Message>>()
                .SingleInstance();

            // IMessageConverter<Twin>
            builder.Register(c => new TwinMessageConverter())
                .As<Core.IMessageConverter<Twin>>()
                .SingleInstance();

            // ICloudProxyProvider
            builder.Register(c => new CloudProxyProvider(c.Resolve<Core.IMessageConverter<Message>>(), c.Resolve<Core.IMessageConverter<Twin>>()))
                .As<ICloudProxyProvider>()
                .SingleInstance();

            // IConnectionManager
            builder.Register(c => new ConnectionManager(c.Resolve<ICloudProxyProvider>()))
                .As<IConnectionManager>()
                .SingleInstance();

            // IEndpointFactory
            builder.Register(c => new EndpointFactory(c.Resolve<IConnectionManager>(), c.Resolve<Core.IMessageConverter<IRoutingMessage>>(), this.edgeDeviceId))
                .As<IEndpointFactory>()
                .SingleInstance();

            // RouterFactory
            builder.Register(c => new EdgeRouteFactory(c.Resolve<IEndpointFactory>()))
                .As<RouteFactory>()
                .SingleInstance();

            // EndpointExecutorConfig
            builder.Register(
                    c =>
                    {
                        RetryStrategy defaultRetryStrategy = new FixedInterval(0, TimeSpan.FromSeconds(1));
                        TimeSpan defaultRevivePeriod = TimeSpan.FromHours(1);
                        TimeSpan defaultTimeout = TimeSpan.FromSeconds(60);
                        return new EndpointExecutorConfig(defaultTimeout, defaultRetryStrategy, defaultRevivePeriod, true);
                    })
                .As<EndpointExecutorConfig>()
                .SingleInstance();

            // IEndpointExecutorFactory
            builder.Register(c => new SyncEndpointExecutorFactory(c.Resolve<EndpointExecutorConfig>()))
                .As<IEndpointExecutorFactory>()
                .SingleInstance();

            // RouterConfig
            builder.Register(c => new RouterConfig(c.Resolve<RouteFactory>().Create(this.routes)))
                .As<RouterConfig>()
                .SingleInstance();

            // Task<Router>
            builder.Register(c => Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, c.Resolve<RouterConfig>(), c.Resolve<IEndpointExecutorFactory>()))
                .As<Task<Router>>()
                .SingleInstance();

            // Task<IEdgeHub>
            builder.Register(
                    async c =>
                    {
                        Router router = await c.Resolve<Task<Router>>();
                        IEdgeHub hub = new RoutingEdgeHub(router, c.Resolve<Core.IMessageConverter<IRoutingMessage>>());
                        return hub;
                    })
                .As<Task<IEdgeHub>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}