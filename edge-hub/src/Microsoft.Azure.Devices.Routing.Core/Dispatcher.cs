// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class Dispatcher : IDisposable
    {
        readonly AtomicBoolean closed;
        readonly CancellationTokenSource cts;
        readonly IEndpointExecutorFactory endpointExecutorFactory;
        readonly AtomicReference<ImmutableDictionary<string, IEndpointExecutor>> executors;
        readonly ICheckpointer checkpointer;
        readonly AsyncLock sync = new AsyncLock();
        readonly string iotHubName;
        static readonly ICollection<IMessage> EmptyMessages = ImmutableList<IMessage>.Empty;

        public string Id { get; }

        public IEnumerable<Endpoint> Endpoints => this.Executors.Values.Select(ex => ex.Endpoint);

        public Option<long> Offset => this.checkpointer.Offset > Checkpointer.InvalidOffset ? Option.Some(this.checkpointer.Offset) : Option.None<long>();

        ImmutableDictionary<string, IEndpointExecutor> Executors => this.executors;

        Dispatcher(string id, string iotHubName, IEnumerable<IEndpointExecutor> execs, IEndpointExecutorFactory endpointExecutorFactory, ICheckpointer checkpointer)
        {
            this.Id = Preconditions.CheckNotNull(id);
            this.iotHubName = Preconditions.CheckNotNull(iotHubName);
            this.endpointExecutorFactory = Preconditions.CheckNotNull(endpointExecutorFactory);
            this.closed = new AtomicBoolean(false);
            this.cts = new CancellationTokenSource();
            this.checkpointer = Preconditions.CheckNotNull(checkpointer);

            ImmutableDictionary<string, IEndpointExecutor> execsDict =  Preconditions.CheckNotNull(execs)
                .ToImmutableDictionary(key => key.Endpoint.Id, value => value);
            this.executors = new AtomicReference<ImmutableDictionary<string, IEndpointExecutor>>(execsDict);
        }

        public static async Task<Dispatcher> CreateAsync(string id, string iotHubName, ISet<Endpoint> endpoints, IEndpointExecutorFactory factory)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(endpoints);
            Preconditions.CheckNotNull(factory);

            IEnumerable<Task<IEndpointExecutor>> tasks = Preconditions.CheckNotNull(endpoints)
                .Select(endpoint => factory.CreateAsync(endpoint));
            IEndpointExecutor[] executors = await Task.WhenAll(tasks);
            return new Dispatcher(id, iotHubName, executors, factory, new NullCheckpointer());
        }

        public static async Task<Dispatcher> CreateAsync(string id, string iotHubName, ISet<Endpoint> endpoints, IEndpointExecutorFactory factory, ICheckpointStore checkpointStore)
        {
            Preconditions.CheckNotNull(id);
            Preconditions.CheckNotNull(endpoints);
            Preconditions.CheckNotNull(factory);
            Preconditions.CheckNotNull(checkpointStore);

            MasterCheckpointer masterCheckpointer = await MasterCheckpointer.CreateAsync(id, checkpointStore);
            var executorFactory = new CheckpointerEndpointExecutorFactory(id, factory, masterCheckpointer);

            IEnumerable<Task<IEndpointExecutor>> tasks = endpoints.Select(endpoint => executorFactory.CreateAsync(endpoint));
            IEndpointExecutor[] executors = await Task.WhenAll(tasks);
            return new Dispatcher(id, iotHubName, executors, executorFactory, masterCheckpointer);
        }

        public Task DispatchAsync(IMessage message, ISet<Endpoint> endpoints)
        {
            this.CheckClosed();

            if (endpoints.Any())
            {
                IList<Task> tasks = new List<Task>();
                // TODO handle case where endpoint is not in dispatcher's list of endpoints
                foreach (Endpoint endpoint in endpoints)
                {
                    IEndpointExecutor exec;
                    if (this.Executors.TryGetValue(endpoint.Id, out exec))
                    {
                        tasks.Add(this.DispatchInternal(exec, message));
                    }
                }

                return Task.WhenAll(tasks);
            }
            else
            {
                Events.UnmatchedMessage(this.iotHubName, message);
                return this.checkpointer.CommitAsync(new[] { message }, EmptyMessages, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
            }
        }

        async Task DispatchInternal(IEndpointExecutor exec, IMessage message)
        {
            try
            {
                await exec.Invoke(message);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is OperationCanceledException)
            {
                // disabled
                // Executor is closed, ignore the send
                // TODO add logging?
            }
        }

        public async Task SetEndpoint(Endpoint endpoint)
        {
            Preconditions.CheckNotNull(endpoint);
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                await this.SetEndpointInternal(endpoint);
            }
        }

        public async Task SetEndpoints(IEnumerable<Endpoint> endpoints)
        {
            this.CheckClosed();
            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();
                foreach (Endpoint endpoint in endpoints)
                {
                    await this.SetEndpointInternal(endpoint);
                }
            }
        }

        async Task SetEndpointInternal(Endpoint endpoint)
        {
            IEndpointExecutor executor;
            ImmutableDictionary<string, IEndpointExecutor> snapshot = this.executors;
            if (!snapshot.TryGetValue(endpoint.Id, out executor))
            {
                executor = await this.endpointExecutorFactory.CreateAsync(endpoint);
                if (!this.executors.CompareAndSet(snapshot, snapshot.Add(endpoint.Id, executor)))
                {
                    throw new InvalidOperationException($"Invalid set endpoint operation for executor {endpoint.Id}");
                }
            }
            else
            {
                await executor.SetEndpoint(endpoint);
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

        public async Task ReplaceEndpoints(ISet<Endpoint> newEndpoints)
        {
            Preconditions.CheckNotNull(newEndpoints);
            this.CheckClosed();

            using (await this.sync.LockAsync(this.cts.Token))
            {
                this.CheckClosed();

                // Remove endpoints not in the new endpoints set
                // Can't use Task.WhenAll because access to the executors dict must be serialized
                IEnumerable<Endpoint> removedEndpoints = this.Endpoints.Except(newEndpoints);
                foreach (Endpoint endpoint in removedEndpoints)
                {
                    await this.RemoveEndpointInternal(endpoint.Id);
                }

                // Set all of the new endpoints
                // Can't use Task.WhenAll because access to the executors dict must be serialized
                foreach (Endpoint endpoint in newEndpoints)
                {
                    await this.SetEndpointInternal(endpoint);
                }
            }
        }

        void CheckClosed()
        {
            if (this.closed)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "{0} is closed.", this));
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
                    await this.checkpointer.CloseAsync(token);
                }
            }
        }

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            Debug.Assert(this.closed);
            if (disposing)
            {
                ImmutableDictionary<string, IEndpointExecutor> snapshot = this.executors;
                foreach (IEndpointExecutor executor in snapshot.Values)
                {
                    executor.Dispose();
                }
                this.checkpointer.Dispose();
                this.cts.Dispose();
                this.sync.Dispose();
            }
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "Dispatcher({0})", this.Id);

        class CheckpointerEndpointExecutorFactory : IEndpointExecutorFactory
        {
            readonly string idPrefix;
            readonly IEndpointExecutorFactory executorFactory;
            readonly ICheckpointerFactory checkpointerFactory;

            public CheckpointerEndpointExecutorFactory(string idPrefix, IEndpointExecutorFactory executorFactory, ICheckpointerFactory checkpointerFactory)
            {
                this.idPrefix = Preconditions.CheckNotNull(idPrefix);
                this.executorFactory = Preconditions.CheckNotNull(executorFactory);
                this.checkpointerFactory = Preconditions.CheckNotNull(checkpointerFactory);
            }

            public async Task<IEndpointExecutor> CreateAsync(Endpoint endpoint)
            {
                string id = RoutingIdBuilder.Parse(this.idPrefix).Map(prefixTemplate => new RoutingIdBuilder(prefixTemplate.IotHubName, prefixTemplate.RouterNumber, Option.Some(endpoint.Id)).GetId()).GetOrElse(endpoint.Id);
                ICheckpointer checkpointer = await this.checkpointerFactory.CreateAsync(id);
                IEndpointExecutor executor = await this.executorFactory.CreateAsync(endpoint, checkpointer);
                return executor;
            }

            public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer)
            {
                return this.executorFactory.CreateAsync(endpoint, checkpointer);
            }

            public Task<IEndpointExecutor> CreateAsync(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig endpointExecutorConfig)
            {
                return this.executorFactory.CreateAsync(endpoint, checkpointer, endpointExecutorConfig);
            }
        }

        static class Events
        {
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<Dispatcher>();
            const int IdStart = Routing.EventIds.Dispatcher;

            enum EventIds
            {
                CounterFailed = IdStart,
            }

            public static void UnmatchedMessage(string iotHubName, IMessage message)
            {
                if (!Routing.PerfCounter.LogUnmatchedMessages(iotHubName, message.MessageSource.ToStringEx(), 1, out string error))
                {
                    Log.LogError((int)EventIds.CounterFailed, "[LogMessageUnmatchedMessagesCounterFailed] {0}", error);
                }

                // Only telemetry messages should be marked as orphaned for user logging / metric purposes.
                if (message.MessageSource == MessageSource.Telemetry)
                {
                    Routing.UserMetricLogger.LogEgressMetric(1, iotHubName, MessageRoutingStatus.Orphaned, MessageSource.Telemetry);
                    Routing.UserAnalyticsLogger.LogOrphanedMessage(iotHubName, message);
                }
            }
        }

    }
}