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
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
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
        readonly StoreAndForwardConfiguration storeAndForwardConfiguration;
        readonly int connectionPoolSize;

        public RoutingModule(string iotHubName, string edgeDeviceId, IEnumerable<string> routes, StoreAndForwardConfiguration storeAndForwardConfiguration, int connectionPoolSize)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.storeAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            this.connectionPoolSize = connectionPoolSize;
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

            // IMessageConverter<TwinCollection>
            builder.Register(c => new TwinCollectionMessageConverter())
                .As<Core.IMessageConverter<TwinCollection>>()
                .SingleInstance();

            // IMessageConverterProvider
            builder.Register(c => new MessageConverterProvider(new Dictionary<Type, IMessageConverter>()
            {
                { typeof(Message), c.Resolve<Core.IMessageConverter<Message>>() },
                { typeof(Twin), c.Resolve<Core.IMessageConverter<Twin>>() },
                { typeof(TwinCollection), c.Resolve<Core.IMessageConverter<TwinCollection>>() }
            }))
                .As<Core.IMessageConverterProvider>()
                .SingleInstance();

            // ICloudProxyProvider
            builder.Register(c => new CloudProxyProvider(c.Resolve<Core.IMessageConverterProvider>(), this.connectionPoolSize))
                .As<ICloudProxyProvider>()
                .SingleInstance();

            // IConnectionManager
            builder.Register(c => new ConnectionManager(c.Resolve<ICloudProxyProvider>(), this.edgeDeviceId))
                .As<IConnectionManager>()
                .SingleInstance();

            // IEndpointFactory
            builder.Register(c => new EndpointFactory(c.Resolve<IConnectionManager>(), c.Resolve<Core.IMessageConverter<IRoutingMessage>>(), this.edgeDeviceId))
                .As<IEndpointFactory>()
                .SingleInstance();

            // RouteFactory
            builder.Register(c => new EdgeRouteFactory(c.Resolve<IEndpointFactory>()))
                .As<RouteFactory>()
                .SingleInstance();

            // RouterConfig
            builder.Register(c => new RouterConfig(c.Resolve<RouteFactory>().Create(this.routes)))
                .As<RouterConfig>()
                .SingleInstance();

            if (!this.storeAndForwardConfiguration.IsEnabled)
            {
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

                // Task<Router>
                builder.Register(c => Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, c.Resolve<RouterConfig>(), c.Resolve<IEndpointExecutorFactory>()))
                    .As<Task<Router>>()
                    .SingleInstance();
            }
            else
            {
                // EndpointExecutorConfig
                builder.Register(
                        c =>
                        {
                            // Endpoint executor config values - 
                            // ExponentialBackoff - minBackoff = 10s, maxBackoff = 60s, delta (used to add randomness to backoff) - 10s (default)
                            // Num of retries = (Store and forward timeout secs / minBackoff) + 1 (this gives us an upper limit on the number of times we need to retry)
                            // Revive period - period for which the endpoint should be considered dead if it doesn't respond - 1 min (we want to try continuously till the message expires)
                            // Timeout - time for which we want for the ack from the endpoint = 60s

                            int minWaitSecs = 10;
                            int maxWaitSecs = 60;
                            int retries = (int)Math.Min(1000, (this.storeAndForwardConfiguration.TimeToLive.TotalSeconds / minWaitSecs) + 1);
                            RetryStrategy retryStrategy = new ExponentialBackoff(retries, TimeSpan.FromSeconds(minWaitSecs), TimeSpan.FromSeconds(maxWaitSecs), TimeSpan.FromSeconds(10));
                            TimeSpan revivePeriod = TimeSpan.FromMinutes(1);
                            TimeSpan timeout = TimeSpan.FromSeconds(60);
                            return new EndpointExecutorConfig(timeout, retryStrategy, revivePeriod);
                        })
                    .As<EndpointExecutorConfig>()
                    .SingleInstance();

                // RouterConfig
                builder.Register(c => new RouterConfig(c.Resolve<RouteFactory>().Create(this.routes)))
                    .As<RouterConfig>()
                    .SingleInstance();

                if (!this.storeAndForwardConfiguration.IsEnabled)
                {
                    // IEndpointExecutorFactory
                    builder.Register(c => new SyncEndpointExecutorFactory(c.Resolve<EndpointExecutorConfig>()))
                        .As<IEndpointExecutorFactory>()
                        .SingleInstance();

                    // Task<Router>
                    builder.Register(c => Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, c.Resolve<RouterConfig>(), c.Resolve<IEndpointExecutorFactory>()))
                        .As<Task<Router>>()
                        .SingleInstance();

                    // ITwinManager
                    builder.Register(c => TwinManager.CreateTwinManager(c.Resolve<IConnectionManager>(), c.Resolve<IMessageConverterProvider>(), Option.None<IStoreProvider>()))
                        .As<ITwinManager>()
                        .SingleInstance();
                }
                else
                {
                    // IDbStore
                    builder.Register(
                        c =>
                        {
                            // Create partitions for messages and twins
                            var partitionsList = new List<string> { Core.Constants.MessageStorePartitionKey, Core.Constants.TwinStorePartitionKey, Core.Constants.CheckpointStorePartitionKey };

                            return new InMemoryDbStoreProvider();
                            //return Storage.RocksDb.DbStoreProvider.Create(this.storeAndForwardConfiguration.StoragePath, partitionsList);
                        })
                        .As<IDbStoreProvider>()
                        .SingleInstance();

                    // ICheckpointStore
                    builder.Register(c => CheckpointStore.Create(c.Resolve<IDbStoreProvider>()))
                        .As<ICheckpointStore>()
                        .SingleInstance();

                    // IEndpointExecutorFactory
                    builder.Register(
                        async c =>
                        {
                            IStoreProvider storeProvider = new StoreProvider(c.Resolve<IDbStoreProvider>());
                            RouterConfig routerConfig = c.Resolve<RouterConfig>();
                            IEnumerable<string> endpoints = routerConfig.Endpoints.Select(ep => ep.Id).Distinct(StringComparer.OrdinalIgnoreCase);
                            IMessageStore messageStore = await MessageStore.CreateAsync(storeProvider, endpoints, c.Resolve<ICheckpointStore>(), this.storeAndForwardConfiguration.TimeToLive);
                            IEndpointExecutorFactory endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(c.Resolve<EndpointExecutorConfig>(), new AsyncEndpointExecutorOptions(1), messageStore);
                            return endpointExecutorFactory;
                        })
                    .As<Task<IEndpointExecutorFactory>>()
                    .SingleInstance();

                    // Task<Router>
                    builder.Register(
                        async c =>
                        {
                            IEndpointExecutorFactory endpointExecutorFactory = await c.Resolve<Task<IEndpointExecutorFactory>>();
                            return await Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, c.Resolve<RouterConfig>(), endpointExecutorFactory, c.Resolve<ICheckpointStore>());
                        })
                        .As<Task<Router>>()
                        .SingleInstance();

                    // ITwinManager
                    builder.Register(c => TwinManager.CreateTwinManager(c.Resolve<IConnectionManager>(), c.Resolve<IMessageConverterProvider>(), Option.Some<IStoreProvider>(new StoreProvider(c.Resolve<IDbStoreProvider>()))))
                        .As<ITwinManager>()
                        .SingleInstance();
                }

                // Task<IEdgeHub>
                builder.Register(
                        async c =>
                        {
                            Router router = await c.Resolve<Task<Router>>();
                            IEdgeHub hub = new RoutingEdgeHub(router, c.Resolve<Core.IMessageConverter<IRoutingMessage>>(), c.Resolve<IConnectionManager>(), c.Resolve<ITwinManager>());
                            return hub;
                        })
                    .As<Task<IEdgeHub>>()
                    .SingleInstance();

                base.Load(builder);
            }
        }
    }
}
