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
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Storage;
    using Microsoft.Azure.Devices.Edge.Hub.Mqtt;
    using Microsoft.Azure.Devices.Edge.Storage;
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
        readonly IDictionary<string, string> routes;
        readonly StoreAndForwardConfiguration storeAndForwardConfiguration;
        readonly int connectionPoolSize;
        readonly bool isStoreAndForwardEnabled;
        readonly string edgeHubConnectionString;

        public RoutingModule(string iotHubName,
            string edgeDeviceId,
            string edgeHubConnectionString,
            IDictionary<string, string> routes,
            bool isStoreAndForwardEnabled,
            StoreAndForwardConfiguration storeAndForwardConfiguration,
            int connectionPoolSize)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            this.edgeHubConnectionString = Preconditions.CheckNonWhiteSpace(edgeHubConnectionString, nameof(edgeHubConnectionString));
            this.routes = Preconditions.CheckNotNull(routes, nameof(routes));
            this.storeAndForwardConfiguration = Preconditions.CheckNotNull(storeAndForwardConfiguration, nameof(storeAndForwardConfiguration));
            this.isStoreAndForwardEnabled = isStoreAndForwardEnabled;
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
            builder.Register(
                c => new MessageConverterProvider(new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(Message), c.Resolve<Core.IMessageConverter<Message>>() },
                    { typeof(Twin), c.Resolve<Core.IMessageConverter<Twin>>() },
                    { typeof(TwinCollection), c.Resolve<Core.IMessageConverter<TwinCollection>>() }
                }))
                .As<Core.IMessageConverterProvider>()
                .SingleInstance();

            // ICloudProxyProvider
            builder.Register(c => new CloudProxyProvider(c.Resolve<Core.IMessageConverterProvider>(), this.connectionPoolSize, !this.isStoreAndForwardEnabled))
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

            // RouteFactory
            builder.Register(c => new EdgeRouteFactory(c.Resolve<IEndpointFactory>()))
                .As<RouteFactory>()
                .SingleInstance();

            // IConfigSource
            builder.Register(
                async c =>
                {
                    IIdentityFactory identityFactory = c.Resolve<IIdentityFactory>();
                    Try<IIdentity> edgeHubIdentity = identityFactory.GetWithSasToken(this.edgeHubConnectionString);
                    if (!edgeHubIdentity.Success)
                    {
                        throw edgeHubIdentity.Exception;
                    }
                    var connectionManager = c.Resolve<IConnectionManager>();
                    var twinCollectionMessageConverter = c.Resolve<Core.IMessageConverter<TwinCollection>>();
                    var twinMessageConverter = c.Resolve<Core.IMessageConverter<Twin>>();
                    var routeFactory = c.Resolve<RouteFactory>();
                    IConfigSource edgeHubConnection = await EdgeHubConnection.Create(edgeHubIdentity.Value, connectionManager, routeFactory, twinCollectionMessageConverter, twinMessageConverter);
                    return edgeHubConnection;
                })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // RouterConfig
            builder.Register(c => new RouterConfig(Enumerable.Empty<Route>()))
                .As<RouterConfig>()
                .SingleInstance();

            if (!this.isStoreAndForwardEnabled)
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
                builder.Register(
                    async c =>
                    {
                        IEndpointExecutorFactory endpointExecutorFactory = c.Resolve<IEndpointExecutorFactory>();
                        RouterConfig routerConfig = c.Resolve<RouterConfig>();
                        Router router = await Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, routerConfig, endpointExecutorFactory);
                        return router;
                    })
                    .As<Task<Router>>()
                    .SingleInstance();

                // ITwinManager
                builder.Register(c => TwinManager.CreateTwinManager(c.Resolve<IConnectionManager>(), c.Resolve<IMessageConverterProvider>(), Option.None<IStoreProvider>()))
                    .As<ITwinManager>()
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
                        // Num of retries = 25
                        // Revive period - period for which the endpoint should be considered dead if it doesn't respond - 1 min (we want to try continuously till the message expires)
                        // Timeout - time for which we want for the ack from the endpoint = 60s
                        // TODO - Num of retries should be set to (Store and forward timeout secs / minBackoff) + 1 (this gives us an upper limit on the number of times we need to retry)
                        // Need to make the number of retries dynamically configurable for that.

                        int minWaitSecs = 10;
                        int maxWaitSecs = 60;
                        int retries = 20;
                        RetryStrategy retryStrategy = new ExponentialBackoff(retries, TimeSpan.FromSeconds(minWaitSecs), TimeSpan.FromSeconds(maxWaitSecs), TimeSpan.FromSeconds(10));
                        TimeSpan revivePeriod = TimeSpan.FromMinutes(1);
                        TimeSpan timeout = TimeSpan.FromSeconds(60);
                        return new EndpointExecutorConfig(timeout, retryStrategy, revivePeriod);
                    })
                    .As<EndpointExecutorConfig>()
                    .SingleInstance();


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

                // IMessageStore
                builder.Register(
                   c =>
                   {
                       ICheckpointStore checkpointStore = c.Resolve<ICheckpointStore>();
                       IDbStoreProvider dbStoreProvider = c.Resolve<IDbStoreProvider>();
                       IStoreProvider storeProvider = new StoreProvider(dbStoreProvider);
                       IMessageStore messageStore = new MessageStore(storeProvider, checkpointStore, TimeSpan.MaxValue);
                       return messageStore;
                   })
                  .As<IMessageStore>()
                  .SingleInstance();

                // IEndpointExecutorFactory
                builder.Register(
                    c =>
                    {
                        EndpointExecutorConfig endpointExecutorConfig = c.Resolve<EndpointExecutorConfig>();
                        IMessageStore messageStore =c.Resolve<IMessageStore>();
                        IEndpointExecutorFactory endpointExecutorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(1), messageStore);
                        return endpointExecutorFactory;
                    })
                   .As<IEndpointExecutorFactory>()
                   .SingleInstance();

                // Task<Router>
                builder.Register(
                    async c =>
                    {
                        ICheckpointStore checkpointStore = c.Resolve<ICheckpointStore>();
                        RouterConfig routerConfig = c.Resolve<RouterConfig>();
                        IEndpointExecutorFactory endpointExecutorFactory = c.Resolve<IEndpointExecutorFactory>();
                        return await Router.CreateAsync(Guid.NewGuid().ToString(), this.iotHubName, routerConfig, endpointExecutorFactory, checkpointStore);
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

            builder.Register(
                async c =>
                {
                    IMessageStore messageStore = this.isStoreAndForwardEnabled ? c.Resolve<IMessageStore>() : null;
                    Router router = await c.Resolve<Task<Router>>();
                    var configUpdater = new ConfigUpdater(router, messageStore);
                    return configUpdater;
                })
                .As<Task<ConfigUpdater>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}