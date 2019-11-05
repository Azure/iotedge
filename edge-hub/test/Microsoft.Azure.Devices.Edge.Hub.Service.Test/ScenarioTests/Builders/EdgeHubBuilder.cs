// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Twin;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public class EdgeHubBuilder
    {
        private ConnectionManagerBuilder connectionManagerBuilder = new ConnectionManagerBuilder().WithNoDevices();
        private RouterBuilder routerBuilder = new RouterBuilder();

        private TimeSpan connectivityCheckFrequency = TimeSpan.FromSeconds(300);
        private TimeSpan disconnectedCheckFrequency = TimeSpan.FromMinutes(2);

        private TimeSpan minTwinSyncPeriod = TimeSpan.FromSeconds(20);

        private bool useV1TwinManager = false;

        public static EdgeHubBuilder Create() => new EdgeHubBuilder();

        public EdgeHubBuilder WithConnectionManager(Func<ConnectionManagerBuilder, ConnectionManagerBuilder> connectionManager)
        {
            connectionManager(this.connectionManagerBuilder);
            return this;
        }

        public EdgeHubBuilder WithRouter(Func<RouterBuilder, RouterBuilder> router)
        {
            router(this.routerBuilder);
            return this;
        }

        public EdgeHubBuilder WithConnectivityCheckFrequency(TimeSpan frequency)
        {
            this.connectivityCheckFrequency = frequency;
            return this;
        }

        public EdgeHubBuilder WithDisconnectedCheckFrequency(TimeSpan frequency)
        {
            this.disconnectedCheckFrequency = frequency;
            return this;
        }

        public EdgeHubBuilder WithMinTwinSyncPeriod(TimeSpan frequency)
        {
            this.minTwinSyncPeriod = frequency;
            return this;
        }

        public EdgeHubBuilder WithV1TwinManager(bool useV1TwinManager)
        {
            this.useV1TwinManager = useV1TwinManager;
            return this;
        }

        public RoutingEdgeHub Build()
        {
            var connectionManager = this.connectionManagerBuilder.Build();
            var invokeMethodHandler = new InvokeMethodHandler(connectionManager);
            var identity = new ModuleIdentity(TestContext.IotHubName, TestContext.DeviceId, "$edgeHub");

            var deviceConnectivityManager = new DeviceConnectivityManager(this.connectivityCheckFrequency, this.disconnectedCheckFrequency, identity);
            deviceConnectivityManager.SetConnectionManager(connectionManager);

            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);
            var messageConverter = new RoutingMessageConverter();
            var router = this.routerBuilder.WithConnectionManager(connectionManager).Build();

            var messageConverterProvider = new MessageConverterProvider(
                                                    new Dictionary<Type, IMessageConverter>()
                                                    {
                                                        [typeof(Twin)] = new TwinMessageConverter(),
                                                        [typeof(TwinCollection)] = new TwinCollectionMessageConverter()
                                                    });

            var storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            var entityStore = storeProvider.GetEntityStore<string, TwinStoreEntity>("EdgeTwin");

            var twinManager = default(ITwinManager);

            if (this.useV1TwinManager)
            {
                twinManager = TwinManager.CreateTwinManager(connectionManager, messageConverterProvider, Option.None<IStoreProvider>());
            }
            else
            {
                twinManager = StoringTwinManager.Create(
                                connectionManager,
                                messageConverterProvider,
                                entityStore,
                                deviceConnectivityManager,
                                new ReportedPropertiesValidator(),
                                Option.Some(this.minTwinSyncPeriod),
                                Option.None<TimeSpan>());
            }

            var result = new RoutingEdgeHub(router, messageConverter, connectionManager, twinManager, TestContext.DeviceId, invokeMethodHandler, subscriptionProcessor);

            var cloudConnectionProvider = connectionManager.AsPrivateAccessible().cloudConnectionProvider as ICloudConnectionProvider;
            cloudConnectionProvider.BindEdgeHub(result);

            return result;
        }
    }
}
