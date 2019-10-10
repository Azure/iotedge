// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;

    public class RouterBuilder
    {
        private readonly List<Route> explicitRoutes = new List<Route>();
        private readonly List<RouteBuilder> toBeBuiltRoutes = new List<RouteBuilder>();

        // the default behavior is that if nothing specified, this class adds a route
        // however if the user explicitly says they don't need any route, this flag shows that
        private bool withRoutes = true;

        private string id = "test-router-" + NextRouterNumber();
        private string iotHubName = "test-hub";

        private Option<Route> fallback = Option.None<Route>();

        IEndpointExecutorFactory executorFactory;

        static int routerCounter;
        static int NextRouterNumber() => Interlocked.Increment(ref routerCounter);

        public static RouterBuilder Create() => new RouterBuilder();

        public RouterBuilder()
        {
            this.SetupDefaultExecutorStrategy();
        }

        public RouterBuilder WithId(string id)
        {
            this.id = id;
            return this;
        }

        public RouterBuilder WithHubName(string iotHubName)
        {
            this.iotHubName = iotHubName;
            return this;
        }

        public RouterBuilder WithNoRoutes()
        {
            this.withRoutes = false;
            return this;
        }

        public RouterBuilder WithFallback(Route fallback)
        {
            this.fallback = Option.Some(fallback);
            return this;
        }

        public RouterBuilder WithRoute(Route route)
        {
            this.explicitRoutes.Add(route);
            return this;
        }

        public RouterBuilder WithRoute(Func<RouteBuilder, RouteBuilder> builder)
        {
            this.toBeBuiltRoutes.Add(builder(RouteBuilder.Create()));
            return this;
        }

        public RouterBuilder WithEndpointExecutorFactory(IEndpointExecutorFactory executorFactory)
        {
            this.executorFactory = executorFactory;
            return this;
        }

        public Router Build()
        {
            var routes = new List<Route>();
            if (this.withRoutes)
            {
                routes.AddRange(this.explicitRoutes);
                routes.AddRange(this.toBeBuiltRoutes.Select(b => b.Build()));
            }

            var routerConfig = new RouterConfig(routes.SelectMany(r => r.Endpoints), routes, this.fallback);
            var result = Router.CreateAsync(
                                    this.id,
                                    this.iotHubName,
                                    routerConfig,
                                    this.executorFactory).Result;

            return result;
        }

        private void SetupDefaultExecutorStrategy()
        {
            var retryStrategy = new ExponentialBackoff(int.MaxValue, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));
            var endpointExecutorConfig = new EndpointExecutorConfig(TimeSpan.FromSeconds(30), retryStrategy, TimeSpan.FromSeconds(30));

            var dbStoreProvider = new Storage.InMemoryDbStoreProvider();
            var storeProvider = new Storage.StoreProvider(dbStoreProvider);
            var checkpointStore = Core.Storage.CheckpointStore.Create(storeProvider);
            var messageStore = new Core.Storage.MessageStore(storeProvider, checkpointStore, TimeSpan.MaxValue);

            this.executorFactory = new StoringAsyncEndpointExecutorFactory(endpointExecutorConfig, new AsyncEndpointExecutorOptions(10, TimeSpan.FromSeconds(10)), messageStore);
        }
    }
}
