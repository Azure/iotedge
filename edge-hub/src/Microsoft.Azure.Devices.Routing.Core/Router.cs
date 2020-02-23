// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Extensions.Logging;

    public class Router : IDisposable
    {
        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;
        readonly Dispatcher dispatcher;
        readonly Evaluator evaluator;
        readonly AtomicReference<ImmutableDictionary<string, Route>> routes;
        readonly AsyncLock sync = new AsyncLock();
        readonly string iotHubName;

        Router(string id, string iotHubName, Evaluator evaluator, Dispatcher dispatcher)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.iotHubName = Preconditions.CheckNotNull(iotHubName);
            this.evaluator = Preconditions.CheckNotNull(evaluator);
            this.dispatcher = Preconditions.CheckNotNull(dispatcher);

            ImmutableDictionary<string, Route> routesDict = Preconditions.CheckNotNull(this.evaluator.Routes)
                .ToImmutableDictionary(r => r.Id, r => r);
            this.routes = new AtomicReference<ImmutableDictionary<string, Route>>(routesDict);

            this.closed = new AtomicBoolean(false);
            this.cts = new CancellationTokenSource();
        }

        public string Id { get; }

        public ISet<Route> Routes
        {
            get
            {
                ImmutableDictionary<string, Route> snapshot = this.routes;
                return new HashSet<Route>(snapshot.Values);
            }
        }

        public Option<long> Offset => this.dispatcher.Offset;

        public static async Task<Router> CreateAsync(string id, string iotHubName, RouterConfig config, IEndpointExecutorFactory executorFactory)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(config);
            Preconditions.CheckNotNull(executorFactory);

            var evaluator = new Evaluator(config);
            Dispatcher dispatcher = await Dispatcher.CreateAsync(id, iotHubName, GetEndpointsWithPriority(config.Routes, config.Fallback), executorFactory);
            return new Router(id, iotHubName, evaluator, dispatcher);
        }

        public static async Task<Router> CreateAsync(string id, string iotHubName, RouterConfig config, IEndpointExecutorFactory executorFactory, ICheckpointStore checkpointStore)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(config);
            Preconditions.CheckNotNull(executorFactory);
            Preconditions.CheckNotNull(checkpointStore);

            var evaluator = new Evaluator(config);
            Dispatcher dispatcher = await Dispatcher.CreateAsync(id, iotHubName, GetEndpointsWithPriority(config.Routes, config.Fallback), executorFactory, checkpointStore);
            return new Router(id, iotHubName, evaluator, dispatcher);
        }

        public Task RouteAsync(IMessage message)
        {
            this.CheckClosed();

            // This doesn't need to take the lock.
            // The dispatcher can handle sending to a closed endpoint executor.
            // In fact, we can't take the lock because we need to support the case where an executor
            // blocks accepting messages (buffer is full) but we want to swap out the endpoint
            // to fix whatever is causing the full buffer.
            return this.RouteInternalAsync(message);
        }

        public async Task RouteAsync(IEnumerable<IMessage> messages)
        {
            this.CheckClosed();

            // This doesn't need to take the lock.
            // The dispatcher can handle sending to a closed endpoint executor.
            // In fact, we can't take the lock because we need to support the case where an executor
            // blocks accepting messages (buffer is full) but we want to swap out the endpoint
            // to fix whatever is causing the full buffer.
            foreach (IMessage message in messages)
            {
                await this.RouteInternalAsync(message);
            }
        }

        public Option<Route> GetRoute(string id)
        {
            Route route;
            ImmutableDictionary<string, Route> snapshot = this.routes;
            return snapshot.TryGetValue(id, out route) ? Option.Some(route) : Option.None<Route>();
        }

        public async Task SetRoute(Route route)
        {
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                ImmutableDictionary<string, Route> snapshot = this.routes;
                this.routes.Value = snapshot.SetItem(route.Id, route);
                this.evaluator.SetRoute(route);

                // Get another snapshot since we just added the new route,
                // then recalculate the priorities for the endpoint with
                // the new route taken into account
                snapshot = this.routes;
                IList<uint> priorities = snapshot.Values
                    .Where(r => r.Endpoint == route.Endpoint)
                    .Select(r => r.Priority).ToList();

                await this.dispatcher.SetEndpoint(route.Endpoint, priorities);
            }
        }

        public async Task RemoveRoute(string id)
        {
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                ImmutableDictionary<string, Route> snapshot = this.routes;

                if (snapshot.TryGetValue(id, out Route removedRoute))
                {
                    this.routes.Value = snapshot.Remove(id);
                    this.evaluator.RemoveRoute(id);
                    await this.dispatcher.RemoveEndpoint(removedRoute.Endpoint.Id);
                }
            }
        }

        public async Task ReplaceRoutes(ISet<Route> newRoutes)
        {
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                ImmutableHashSet<Endpoint> endpoints = newRoutes.Select(r => r.Endpoint).ToImmutableHashSet();
                IDictionary<Endpoint, IList<uint>> endpointWithPriority = GetEndpointsWithPriority(newRoutes, Option.None<Route>());
                this.evaluator.ReplaceRoutes(newRoutes);
                await this.dispatcher.ReplaceEndpoints(endpointWithPriority);
                this.routes.Value = newRoutes.ToImmutableDictionary(r => r.Id, r => r);
            }
        }

        public async Task CloseAsync(CancellationToken token)
        {
            if (!this.closed.GetAndSet(true))
            {
                this.cts.Cancel();
                using (await this.sync.LockAsync(CancellationToken.None))
                {
                    await this.evaluator.CloseAsync(token);
                    await this.dispatcher.CloseAsync(token);
                }
            }
        }

        public void Dispose() => this.Dispose(true);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "Router({0})", this.Id);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.cts.Dispose();
                this.dispatcher.Dispose();
                this.sync.Dispose();
            }
        }

        static void MergeEndpointPriorities(Dictionary<Endpoint, IList<uint>> existing, Endpoint endpoint, uint newPriority)
        {
            if (existing.ContainsKey(endpoint))
            {
                // Merge the existing priorities together
                existing[endpoint].Add(newPriority);
            }
            else
            {
                // Add a new endpoint with its priorities
                existing.Add(endpoint, new List<uint>() { newPriority });
            }
        }

        static IDictionary<Endpoint, IList<uint>> GetEndpointsWithPriority(IEnumerable<Route> routes, Option<Route> fallback)
        {
            var endpoints = new Dictionary<Endpoint, IList<uint>>();

            foreach (Route r in routes)
            {
                MergeEndpointPriorities(endpoints, r.Endpoint, r.Priority);
            }

            fallback.ForEach(f => MergeEndpointPriorities(endpoints, f.Endpoint, f.Priority));

            return endpoints;
        }

        Task RouteInternalAsync(IMessage message)
        {
            ISet<RouteResult> results = this.evaluator.Evaluate(message);

            Events.MessageEvaluation(this.iotHubName, message, results);

            return this.dispatcher.DispatchAsync(message, results);
        }

        void CheckClosed()
        {
            if (this.closed)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Router {0} is closed.", this));
            }
        }

        static class Events
        {
            const int IdStart = Routing.EventIds.Router;
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<Router>();

            enum EventIds
            {
                CounterFailed = IdStart,
            }

            public static void MessageEvaluation(string iotHubName, IMessage message, ISet<RouteResult> results)
            {
                if (!Routing.PerfCounter.LogMessageEndpointsMatched(iotHubName, message.MessageSource.ToString(), results.LongCount(), out string error))
                {
                    Log.LogError((int)EventIds.CounterFailed, "[LogMessageEndpointsMatchedCounterFailed] {0}", error);
                }
            }
        }
    }
}
