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
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Extensions.Logging;

    public class Dispatcher : IDisposable
    {
        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;
        readonly IEndpointExecutorFactory endpointExecutorFactory;
        readonly AtomicReference<ImmutableDictionary<string, IEndpointExecutor>> executors;
        readonly MasterCheckpointer masterCheckpointer;
        readonly AsyncLock sync = new AsyncLock();
        readonly string iotHubName;
        static readonly ICollection<IMessage> EmptyMessages = ImmutableList<IMessage>.Empty;

        Dispatcher(string id, string iotHubName, IEnumerable<IEndpointExecutor> execs, IEndpointExecutorFactory endpointExecutorFactory, MasterCheckpointer masterCheckpointer)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.iotHubName = Preconditions.CheckNotNull(iotHubName);
            this.endpointExecutorFactory = Preconditions.CheckNotNull(endpointExecutorFactory);
            this.closed = new AtomicBoolean(false);
            this.cts = new CancellationTokenSource();
            this.masterCheckpointer = Preconditions.CheckNotNull(masterCheckpointer);

            ImmutableDictionary<string, IEndpointExecutor> execsDict = Preconditions.CheckNotNull(execs)
                .ToImmutableDictionary(key => key.Endpoint.Id, value => value);
            this.executors = new AtomicReference<ImmutableDictionary<string, IEndpointExecutor>>(execsDict);
        }

        public string Id { get; }

        public IEnumerable<Endpoint> Endpoints => this.Executors.Values.Select(ex => ex.Endpoint);

        public Option<long> Offset => this.masterCheckpointer.Offset > Checkpointer.InvalidOffset ? Option.Some(this.masterCheckpointer.Offset) : Option.None<long>();

        ImmutableDictionary<string, IEndpointExecutor> Executors => this.executors;

        public static async Task<Dispatcher> CreateAsync(string id, string iotHubName, IDictionary<Endpoint, IList<uint>> endpointsWithPriorities, IEndpointExecutorFactory factory)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(endpointsWithPriorities);
            Preconditions.CheckNotNull(factory);

            var masterCheckpointer = await MasterCheckpointer.CreateAsync(id, NullCheckpointStore.Instance);

            IEnumerable<Task<IEndpointExecutor>> tasks = endpointsWithPriorities.Select(e => factory.CreateAsync(e.Key, e.Value));
            IEndpointExecutor[] executors = await Task.WhenAll(tasks);
            return new Dispatcher(id, iotHubName, executors, factory, masterCheckpointer);
        }

        public static async Task<Dispatcher> CreateAsync(string id, string iotHubName, IDictionary<Endpoint, IList<uint>> endpointsWithPriorities, IEndpointExecutorFactory factory, ICheckpointStore checkpointStore)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(endpointsWithPriorities);
            Preconditions.CheckNotNull(factory);
            Preconditions.CheckNotNull(checkpointStore);

            var masterCheckpointer = await MasterCheckpointer.CreateAsync(id, checkpointStore);

            IEnumerable<Task<IEndpointExecutor>> tasks = endpointsWithPriorities.Select(e => factory.CreateAsync(e.Key, e.Value, masterCheckpointer));
            IEndpointExecutor[] executors = await Task.WhenAll(tasks);
            return new Dispatcher(id, iotHubName, executors, factory, masterCheckpointer);
        }

        public Task DispatchAsync(IMessage message, ISet<RouteResult> routeResults)
        {
            this.CheckClosed();

            if (routeResults.Any())
            {
                IList<Task> tasks = new List<Task>();
                // TODO handle case where endpoint is not in dispatcher's list of endpoints
                foreach (RouteResult result in routeResults)
                {
                    IEndpointExecutor exec;
                    if (this.Executors.TryGetValue(result.Endpoint.Id, out exec))
                    {
                        tasks.Add(this.DispatchInternal(exec, message, result.Priority, result.TimeToLiveSecs));
                    }
                }

                return Task.WhenAll(tasks);
            }
            else
            {
                Events.UnmatchedMessage(this.iotHubName, message);
                return this.masterCheckpointer.CommitAsync(new[] { message }, EmptyMessages, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
            }
        }

        public async Task SetEndpoint(Endpoint endpoint, IList<uint> priorities)
        {
            Preconditions.CheckNotNull(endpoint);
            Preconditions.CheckNotNull(priorities);
            Preconditions.CheckArgument(priorities.Count != 0);
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                await this.SetEndpointInternal(endpoint, priorities);
            }
        }

        public async Task RemoveEndpoint(string id)
        {
            Preconditions.CheckNotNull(id);
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                await this.RemoveEndpointInternal(id);
            }
        }

        public async Task RemoveEndpoints(IEnumerable<string> ids)
        {
            this.CheckClosed();
            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                foreach (string id in ids)
                {
                    await this.RemoveEndpointInternal(id);
                }
            }
        }

        public async Task ReplaceEndpoints(IDictionary<Endpoint, IList<uint>> endpointsWithPriorities)
        {
            Preconditions.CheckNotNull(endpointsWithPriorities);
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();

                // Remove endpoints not in the new endpoints set
                // Can't use Task.WhenAll because access to the executors dict must be serialized
                IEnumerable<Endpoint> removedEndpoints = this.Endpoints.Except(endpointsWithPriorities.Select(e => e.Key).ToImmutableHashSet());
                foreach (Endpoint endpoint in removedEndpoints)
                {
                    await this.RemoveEndpointInternal(endpoint.Id);
                }

                // Set all of the new endpoints
                // Can't use Task.WhenAll because access to the executors dict must be serialized
                foreach (KeyValuePair<Endpoint, IList<uint>> e in endpointsWithPriorities)
                {
                    await this.SetEndpointInternal(e.Key, e.Value);
                }
            }
        }

        public async Task CloseAsync(CancellationToken token)
        {
            using (await this.sync.LockAsync(CancellationToken.None))
            {
                if (!this.closed.GetAndSet(true))
                {
                    this.cts.Cancel();
                    ImmutableDictionary<string, IEndpointExecutor> snapshot = this.executors;
                    foreach (IEndpointExecutor exec in snapshot.Values)
                    {
                        await exec.CloseAsync();
                    }

                    await this.masterCheckpointer.CloseAsync(token);
                }
            }
        }

        public void Dispose() => this.Dispose(true);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "Dispatcher({0})", this.Id);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ImmutableDictionary<string, IEndpointExecutor> snapshot = this.executors;
                foreach (IEndpointExecutor executor in snapshot.Values)
                {
                    executor.Dispose();
                }

                this.masterCheckpointer.Dispose();
                this.cts.Dispose();
                this.sync.Dispose();
            }
        }

        async Task DispatchInternal(IEndpointExecutor exec, IMessage message, uint priority, uint timeToLiveSecs)
        {
            try
            {
                await exec.Invoke(message, priority, timeToLiveSecs);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is OperationCanceledException)
            {
                // disabled
                // Executor is closed, ignore the send
                // TODO add logging?
            }
        }

        async Task SetEndpointInternal(Endpoint endpoint, IList<uint> priorities)
        {
            IEndpointExecutor executor;
            ImmutableDictionary<string, IEndpointExecutor> snapshot = this.executors;
            if (!snapshot.TryGetValue(endpoint.Id, out executor))
            {
                executor = await this.endpointExecutorFactory.CreateAsync(endpoint, priorities, this.masterCheckpointer);
                if (!this.executors.CompareAndSet(snapshot, snapshot.Add(endpoint.Id, executor)))
                {
                    throw new InvalidOperationException($"Invalid set endpoint operation for executor {endpoint.Id}");
                }
            }
            else
            {
                await executor.SetEndpoint(endpoint, priorities);
            }
        }

        async Task RemoveEndpointInternal(string id)
        {
            IEndpointExecutor executor;
            ImmutableDictionary<string, IEndpointExecutor> snapshot = this.executors;
            if (snapshot.TryGetValue(id, out executor))
            {
                if (!this.executors.CompareAndSet(snapshot, snapshot.Remove(id)))
                {
                    throw new InvalidOperationException($"Invalid remove endpoint operation for executor {id}");
                }

                await executor.CloseAsync();
                executor.Dispose();
            }
        }

        void CheckClosed()
        {
            if (this.closed)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0} is closed.", this));
            }
        }

        static class Events
        {
            const int IdStart = Routing.EventIds.Dispatcher;
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<Dispatcher>();

            enum EventIds
            {
                CounterFailed = IdStart,
            }

            public static void UnmatchedMessage(string iotHubName, IMessage message)
            {
                if (!Routing.PerfCounter.LogUnmatchedMessages(iotHubName, message.MessageSource.ToString(), 1, out string error))
                {
                    Log.LogError((int)EventIds.CounterFailed, "[LogMessageUnmatchedMessagesCounterFailed] {0}", error);
                }

                // Only telemetry messages should be marked as orphaned for user logging / metric purposes.
                if (message.MessageSource.IsTelemetry())
                {
                    Routing.UserMetricLogger.LogEgressMetric(1, iotHubName, MessageRoutingStatus.Orphaned, message);
                    Routing.UserAnalyticsLogger.LogOrphanedMessage(iotHubName, message);
                }
            }
        }
    }
}
