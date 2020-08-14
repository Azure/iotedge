// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Timer;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;
    using static System.FormattableString;
    using AsyncLock = Microsoft.Azure.Devices.Edge.Util.Concurrency.AsyncLock;

    public class StoringAsyncEndpointExecutor : IEndpointExecutor
    {
        readonly AtomicBoolean closed = new AtomicBoolean();
        readonly IMessageStore messageStore;
        readonly Task sendMessageTask;
        readonly AsyncManualResetEvent hasMessagesInQueue = new AsyncManualResetEvent(true);
        readonly AsyncEndpointExecutorOptions options;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly ICheckpointerFactory checkpointerFactory;
        readonly EndpointExecutorConfig config;
        AtomicReference<ImmutableDictionary<uint, EndpointExecutorFsm>> prioritiesToFsms;
        EndpointExecutorFsm lastUsedFsm;

        public StoringAsyncEndpointExecutor(
            Endpoint endpoint,
            ICheckpointerFactory checkpointerFactory,
            EndpointExecutorConfig config,
            AsyncEndpointExecutorOptions options,
            IMessageStore messageStore)
        {
            this.Endpoint = Preconditions.CheckNotNull(endpoint);
            this.checkpointerFactory = Preconditions.CheckNotNull(checkpointerFactory);
            this.config = Preconditions.CheckNotNull(config);
            this.options = Preconditions.CheckNotNull(options);
            this.messageStore = messageStore;
            this.sendMessageTask = Task.Run(this.SendMessagesPump);
            this.prioritiesToFsms = new AtomicReference<ImmutableDictionary<uint, EndpointExecutorFsm>>(ImmutableDictionary<uint, EndpointExecutorFsm>.Empty);
        }

        public Endpoint Endpoint { get; }

        public EndpointExecutorStatus Status => this.lastUsedFsm.Status;

        public async Task Invoke(IMessage message, uint priority, uint timeToLiveSecs)
        {
            try
            {
                if (this.closed)
                {
                    throw new InvalidOperationException($"Endpoint executor for endpoint {this.Endpoint} is closed.");
                }

                using (MetricsV0.StoreLatency(this.Endpoint.Id))
                {
                    // Get the checkpointer corresponding to the queue for this priority
                    ImmutableDictionary<uint, EndpointExecutorFsm> snapshot = this.prioritiesToFsms;
                    ICheckpointer checkpointer = snapshot[priority].Checkpointer;

                    IMessage storedMessage = await this.messageStore.Add(GetMessageQueueId(this.Endpoint.Id, priority), message, timeToLiveSecs);
                    checkpointer.Propose(storedMessage);
                    Events.AddMessageSuccess(this, storedMessage.Offset, priority, timeToLiveSecs);
                }

                this.hasMessagesInQueue.Set();
                MetricsV0.StoredCountIncrement(this.Endpoint.Id, priority);
            }
            catch (Exception ex)
            {
                Routing.UserMetricLogger.LogIngressFailureMetric(1, this.Endpoint.IotHubName, message, "storage_failure");
                Events.AddMessageFailure(this, ex);
                throw;
            }
        }

        public void Dispose() => this.Dispose(true);

        public async Task CloseAsync()
        {
            Events.Close(this);

            try
            {
                if (!this.closed.GetAndSet(true))
                {
                    this.cts.Cancel();
                    // Require to close all FSMs to complete currently executing command if any in order to unblock sendMessageTask.
                    ImmutableDictionary<uint, EndpointExecutorFsm> snapshot = this.prioritiesToFsms;
                    foreach (EndpointExecutorFsm fsm in snapshot.Values)
                    {
                        await fsm.CloseAsync();
                    }

                    await (this.messageStore?.RemoveEndpoint(this.Endpoint.Id) ?? Task.CompletedTask);
                    await (this.sendMessageTask ?? Task.CompletedTask);
                }

                Events.CloseSuccess(this);
            }
            catch (Exception ex)
            {
                Events.CloseFailure(this, ex);
                throw;
            }
        }

        public async Task SetEndpoint(Endpoint newEndpoint, IList<uint> priorities)
        {
            Events.SetEndpoint(this);

            try
            {
                Preconditions.CheckNotNull(newEndpoint);
                Preconditions.CheckArgument(newEndpoint.Id.Equals(this.Endpoint.Id), $"Can only set new endpoint with same id. Given {newEndpoint.Id}, expected {this.Endpoint.Id}");
                Preconditions.CheckNotNull(priorities);
                Preconditions.CheckArgument(priorities.Count != 0);

                if (this.closed)
                {
                    throw new InvalidOperationException($"Endpoint executor for endpoint {this.Endpoint} is closed.");
                }

                await this.UpdatePriorities(priorities, Option.Some<Endpoint>(newEndpoint));
                Events.SetEndpointSuccess(this);
            }
            catch (Exception ex)
            {
                Events.SetEndpointFailure(this, ex);
                throw;
            }
        }

        public async Task UpdatePriorities(IList<uint> priorities, Option<Endpoint> newEndpoint)
        {
            Preconditions.CheckArgument(priorities.Count > 0);
            Events.UpdatePriorities(this, priorities);

            if (this.closed)
            {
                throw new InvalidOperationException($"Endpoint executor for endpoint {this.Endpoint} is closed.");
            }

            // Update priorities by merging the new ones with the existing.
            // We don't ever remove stale priorities, otherwise stored messages
            // pending for a removed priority will never get sent.
            ImmutableDictionary<uint, EndpointExecutorFsm> snapshot = this.prioritiesToFsms;
            Dictionary<uint, EndpointExecutorFsm> updatedSnapshot = new Dictionary<uint, EndpointExecutorFsm>(snapshot);
            foreach (uint priority in priorities)
            {
                if (!updatedSnapshot.ContainsKey(priority))
                {
                    string id = GetMessageQueueId(this.Endpoint.Id, priority);

                    // Create a message queue in the store for every priority we have
                    await this.messageStore.AddEndpoint(id);

                    // Create a checkpointer and a FSM for every message queue
                    ICheckpointer checkpointer = await this.checkpointerFactory.CreateAsync(id, this.Endpoint.Id, priority);
                    EndpointExecutorFsm fsm = new EndpointExecutorFsm(this.Endpoint, checkpointer, this.config);

                    // Add it to our dictionary
                    updatedSnapshot.Add(priority, fsm);
                }
                else
                {
                    // Update the existing FSM with the new endpoint
                    EndpointExecutorFsm fsm = updatedSnapshot[priority];
                    await newEndpoint.ForEachAsync(e => fsm.RunAsync(Commands.UpdateEndpoint(e)));
                }
            }

            if (this.prioritiesToFsms.CompareAndSet(snapshot, updatedSnapshot.ToImmutableDictionary()))
            {
                Events.UpdatePrioritiesSuccess(this, updatedSnapshot.Keys.ToList());

                // Update the lastUsedFsm to be the initial one, we always
                // have at least one priority->FSM pair at this point.
                this.lastUsedFsm = updatedSnapshot[priorities[0]];
            }
            else
            {
                Events.UpdatePrioritiesFailure(this, updatedSnapshot.Keys.ToList());
            }
        }

        static string GetMessageQueueId(string endpointId, uint priority)
        {
            if (priority == RouteFactory.DefaultPriority)
            {
                // We need to maintain backwards compatibility
                // for existing sequential stores that don't
                // have the "_Pri<x>" suffix. We use the default
                // priority (2,000,000,000) for this, which means
                // the store ID is just the endpoint ID.
                return endpointId;
            }
            else
            {
                // The actual ID for the underlying store is of string format:
                //      <endpointId>_Pri<priority>
                return endpointId + "_Pri" + priority.ToString();
            }
        }

        async Task SendMessagesPump()
        {
            try
            {
                Events.StartSendMessagesPump(this);
                int batchSize = this.options.BatchSize * this.Endpoint.FanOutFactor;

                // Keep the stores and prefetchers for each priority loaded
                // for the duration of the pump
                var messageProviderPairs = new Dictionary<uint, (IMessageIterator, StoreMessagesProvider)>();

                // Outer loop to maintain the message pump until the executor shuts down
                while (!this.cts.IsCancellationRequested)
                {
                    try
                    {
                        await this.hasMessagesInQueue.WaitAsync(this.options.BatchTimeout);

                        ImmutableDictionary<uint, EndpointExecutorFsm> snapshot = this.prioritiesToFsms;
                        bool haveMessagesRemaining = false;

                        uint[] orderedPriorities = snapshot.Keys.OrderBy(k => k).ToArray();
                        // Iterate through all the message queues in priority order
                        foreach (uint priority in orderedPriorities)
                        {
                            // Also check for cancellation in every inner loop,
                            // since it could take time to send a batch of messages
                            if (this.cts.IsCancellationRequested)
                            {
                                break;
                            }

                            EndpointExecutorFsm fsm = snapshot[priority];

                            // Update the lastUsedFsm to be the current FSM
                            this.lastUsedFsm = fsm;

                            (IMessageIterator, StoreMessagesProvider) pair;
                            if (!messageProviderPairs.TryGetValue(priority, out pair))
                            {
                                // Create and cache a new pair for the message provider
                                // so we can reuse it every loop
                                pair.Item1 = this.messageStore.GetMessageIterator(GetMessageQueueId(this.Endpoint.Id, priority), fsm.Checkpointer.Offset + 1);
                                pair.Item2 = new StoreMessagesProvider(pair.Item1, batchSize);
                                messageProviderPairs.Add(priority, pair);
                            }

                            StoreMessagesProvider storeMessagesProvider = pair.Item2;
                            IMessage[] messages = await storeMessagesProvider.GetMessages();
                            if (messages.Length > 0)
                            {
                                // Tag the message with the priority that we're currently
                                // processing, so it can be used by metrics later
                                foreach (IMessage msg in messages)
                                {
                                    msg.ProcessedPriority = priority;
                                }

                                Events.ProcessingMessages(this, messages, priority);
                                await this.ProcessMessages(messages, fsm);
                                Events.SendMessagesSuccess(this, messages, priority);
                                MetricsV0.DrainedCountIncrement(this.Endpoint.Id, messages.Length, priority);

                                // Only move on to the next priority if the queue for the current
                                // priority is empty. If we processed any messages, break out of
                                // the inner loop to restart at the beginning of the priorities list
                                // again. This is so we can catch and process any higher priority
                                // messages that came in while we were sending the current batch
                                haveMessagesRemaining = true;
                                break;
                            }
                        }

                        if (!haveMessagesRemaining)
                        {
                            // All the message queues have been drained, reset the hasMessagesInQueue flag.
                            this.hasMessagesInQueue.Reset();
                        }
                    }
                    catch (Exception ex)
                    {
                        Events.SendMessagesError(this, ex);
                        // Swallow exception and keep trying.
                    }
                }
            }
            catch (Exception ex)
            {
                Events.SendMessagesPumpFailure(this, ex);
            }
        }

        async Task ProcessMessages(IMessage[] messages, EndpointExecutorFsm fsm)
        {
            SendMessage command = Commands.SendMessage(messages);
            await fsm.RunAsync(command);
            await command.Completion;
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.cts.Dispose();
                ImmutableDictionary<uint, EndpointExecutorFsm> snapshot = this.prioritiesToFsms;
                this.prioritiesToFsms.CompareAndSet(snapshot, ImmutableDictionary<uint, EndpointExecutorFsm>.Empty);
                foreach (KeyValuePair<uint, EndpointExecutorFsm> entry in snapshot)
                {
                    var fsm = entry.Value;
                    fsm.Dispose();
                }
            }
        }

        // This class is used to prefetch messages from the store before they are needed.
        // As soon as the previous batch is consumed, the next batch is fetched.
        // A pump is started as soon as the object is created, and it keeps the messages list populated.
        internal class StoreMessagesProvider
        {
            readonly IMessageIterator iterator;
            readonly int batchSize;
            readonly AsyncLock stateLock = new AsyncLock();
            Task<IList<IMessage>> getMessagesTask;

            public StoreMessagesProvider(IMessageIterator iterator, int batchSize)
            {
                this.iterator = iterator;
                this.batchSize = batchSize;
                this.getMessagesTask = Task.Run(this.GetMessagesFromStore);
            }

            public async Task<IMessage[]> GetMessages()
            {
                using (await this.stateLock.LockAsync())
                {
                    var messages = await this.getMessagesTask;
                    if (messages.Count == 0)
                    {
                        messages = await this.GetMessagesFromStore();
                    }
                    else
                    {
                        this.getMessagesTask = Task.Run(this.GetMessagesFromStore);
                    }

                    return messages.ToArray();
                }
            }

            async Task<IList<IMessage>> GetMessagesFromStore()
            {
                var messagesList = new List<IMessage>();
                while (messagesList.Count < this.batchSize)
                {
                    int curBatchSize = this.batchSize - messagesList.Count;
                    IList<IMessage> messages = (await this.iterator.GetNext(curBatchSize)).ToList();
                    if (!messages.Any())
                    {
                        break;
                    }

                    messagesList.AddRange(messages);
                }

                return messagesList;
            }
        }

        static class Events
        {
            const int IdStart = Routing.EventIds.StoringAsyncEndpointExecutor;
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<StoringAsyncEndpointExecutor>();

            enum EventIds
            {
                AddMessageSuccess = IdStart,
                StartSendMessagesPump,
                SendMessagesError,
                ProcessMessagesSuccess,
                SendMessagesPumpFailure,
                ProcessingMessages,
                SetEndpoint,
                SetEndpointSuccess,
                SetEndpointFailure,
                UpdatePriorities,
                UpdatePrioritiesSuccess,
                UpdatePrioritiesFailure,
                Close,
                CloseSuccess,
                CloseFailure,
                ErrorInPopulatePump
            }

            public static void AddMessageSuccess(StoringAsyncEndpointExecutor executor, long offset, uint priority, uint timeToLiveSecs)
            {
                Log.LogDebug((int)EventIds.AddMessageSuccess, $"[AddMessageSuccess] Successfully added message to store for EndpointId: {executor.Endpoint.Id}, Message offset: {offset}, Priority: {priority}, TTL: {timeToLiveSecs}");
            }

            public static void AddMessageFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.AddMessageSuccess, ex, $"[AddMessageFailure] Error adding added message to store for EndpointId: {executor.Endpoint.Id}");
            }

            public static void StartSendMessagesPump(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.StartSendMessagesPump, $"[StartSendMessagesPump] Starting pump to send stored messages to EndpointId: {executor.Endpoint.Id}.");
            }

            public static void SendMessagesError(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogWarning((int)EventIds.SendMessagesError, ex, $"[SendMessageError] Error sending message batch to endpoint to IPL head for EndpointId: {executor.Endpoint.Id}.");
            }

            public static void SendMessagesSuccess(StoringAsyncEndpointExecutor executor, ICollection<IMessage> messages, uint priority)
            {
                if (messages.Count > 0)
                {
                    Log.LogDebug((int)EventIds.ProcessMessagesSuccess, Invariant($"[ProcessMessagesSuccess] Successfully processed {messages.Count} messages for EndpointId: {executor.Endpoint.Id}, Priority: {priority}."));
                }
            }

            public static void ProcessingMessages(StoringAsyncEndpointExecutor executor, ICollection<IMessage> messages, uint priority)
            {
                if (messages.Count > 0)
                {
                    Log.LogDebug((int)EventIds.ProcessingMessages, Invariant($"[ProcessingMessages] Processing {messages.Count} messages for EndpointId: {executor.Endpoint.Id}, Priority: {priority}"));
                }
            }

            public static void SendMessagesPumpFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogCritical((int)EventIds.SendMessagesPumpFailure, ex, $"[SendMessagesPumpFailure] Unable to start pump to send stored messages for EndpointId: {executor.Endpoint.Id}.");
            }

            public static void SetEndpoint(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpoint, "[SetEndpoint] Set endpoint began. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void SetEndpointSuccess(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.SetEndpointSuccess, "[SetEndpointSuccess] Set endpoint succeeded. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void SetEndpointFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.SetEndpointFailure, ex, "[SetEndpointFailure] Set endpoint failed. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void UpdatePriorities(StoringAsyncEndpointExecutor executor, IList<uint> priorities)
            {
                Log.LogInformation((int)EventIds.UpdatePriorities, $"[UpdatePriorities] Update priorities begin for EndpointId: {executor.Endpoint.Id}, EndpointName: {executor.Endpoint.Name}, Incoming Priorities: {priorities}");
            }

            public static void UpdatePrioritiesSuccess(StoringAsyncEndpointExecutor executor, IList<uint> priorities)
            {
                Log.LogInformation((int)EventIds.UpdatePrioritiesSuccess, $"[UpdatePrioritiesSuccess] Update priorities succeeded EndpointId: {executor.Endpoint.Id}, EndpointName: {executor.Endpoint.Name}, New Priorities: {priorities}");
            }

            public static void UpdatePrioritiesFailure(StoringAsyncEndpointExecutor executor, IList<uint> priorities)
            {
                Log.LogError((int)EventIds.UpdatePrioritiesFailure, $"[UpdatePrioritiesSuccess] Update priorities failed EndpointId: {executor.Endpoint.Id}, EndpointName: {executor.Endpoint.Name}, New Priorities: {priorities}");
            }

            public static void Close(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.Close, "[Close] Close began. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void CloseSuccess(StoringAsyncEndpointExecutor executor)
            {
                Log.LogInformation((int)EventIds.CloseSuccess, "[CloseSuccess] Close succeeded. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void CloseFailure(StoringAsyncEndpointExecutor executor, Exception ex)
            {
                Log.LogError((int)EventIds.CloseFailure, ex, "[CloseFailure] Close failed. EndpointId: {0}, EndpointName: {1}", executor.Endpoint.Id, executor.Endpoint.Name);
            }

            public static void ErrorInPopulatePump(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorInPopulatePump, ex, "Error in populate messages pump");
            }
        }

        static class MetricsV0
        {
            static readonly CounterOptions EndpointMessageStoredCountOptions = new CounterOptions
            {
                Name = "EndpointMessageStoredCount",
                MeasurementUnit = Unit.Events
            };

            static readonly CounterOptions EndpointMessageDrainedCountOptions = new CounterOptions
            {
                Name = "EndpointMessageDrainedCount",
                MeasurementUnit = Unit.Events
            };

            static readonly TimerOptions EndpointMessageLatencyOptions = new TimerOptions
            {
                Name = "EndpointMessageStoredLatencyMs",
                MeasurementUnit = Unit.None,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds
            };

            public static void StoredCountIncrement(string identity, uint priority) => Edge.Util.Metrics.MetricsV0.CountIncrement(GetTagsWithPriority(identity, priority), EndpointMessageStoredCountOptions, 1);

            public static void DrainedCountIncrement(string identity, long amount, uint priority) => Edge.Util.Metrics.MetricsV0.CountIncrement(GetTagsWithPriority(identity, priority), EndpointMessageDrainedCountOptions, amount);

            public static IDisposable StoreLatency(string identity) => Edge.Util.Metrics.MetricsV0.Latency(GetTags(identity), EndpointMessageLatencyOptions);

            internal static MetricTags GetTags(string id)
            {
                return new MetricTags("EndpointId", id);
            }

            internal static MetricTags GetTagsWithPriority(string id, uint priority)
            {
                return new MetricTags(new string[] { "EndpointId", "priority" }, new string[] { id, priority.ToString() });
            }
        }
    }
}
