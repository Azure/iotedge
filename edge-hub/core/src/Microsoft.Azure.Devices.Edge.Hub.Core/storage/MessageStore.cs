// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Timer;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static System.FormattableString;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;
    using SystemProperties = Microsoft.Azure.Devices.Edge.Hub.Core.SystemProperties;

    /// <summary>
    /// This object is responsible for storing messages for each endpoint.
    /// - Each message is stored in the message store
    /// - A reference to the message is also stored in a per-endpoint-queue.
    /// - Messages can be retrieved per endpoint in batches.
    /// </summary>
    public class MessageStore : IMessageStore
    {
        const long DefaultStartingOffset = 0;
        readonly IEntityStore<string, MessageWrapper> messageEntityStore;
        readonly ConcurrentDictionary<string, ISequentialStore<MessageRef>> endpointSequentialStores;
        readonly CleanupProcessor messagesCleaner;
        readonly ICheckpointStore checkpointStore;
        readonly IStoreProvider storeProvider;
        TimeSpan timeToLive;

        public MessageStore(IStoreProvider storeProvider, ICheckpointStore checkpointStore, TimeSpan timeToLive, bool checkEntireQueueOnCleanup, int messageCleanupIntervalSecs)
        {
            this.storeProvider = Preconditions.CheckNotNull(storeProvider);
            this.messageEntityStore = this.storeProvider.GetEntityStore<string, MessageWrapper>(Constants.MessageStorePartitionKey);
            this.endpointSequentialStores = new ConcurrentDictionary<string, ISequentialStore<MessageRef>>();
            this.timeToLive = timeToLive;
            this.checkpointStore = Preconditions.CheckNotNull(checkpointStore, nameof(checkpointStore));
            this.messagesCleaner = new CleanupProcessor(this, checkEntireQueueOnCleanup, messageCleanupIntervalSecs);
            Events.MessageStoreCreated();
        }

        public void SetTimeToLive(TimeSpan timeSpan)
        {
            this.timeToLive = timeSpan;
            Events.TtlUpdated(timeSpan);
        }

        public async Task AddEndpoint(string endpointId)
        {
            CheckpointData checkpointData = await this.checkpointStore.GetCheckpointDataAsync(endpointId, CancellationToken.None);
            ISequentialStore<MessageRef> sequentialStore = await this.storeProvider.GetSequentialStore<MessageRef>(endpointId, checkpointData.Offset + 1);
            if (this.endpointSequentialStores.TryAdd(endpointId, sequentialStore))
            {
                Events.SequentialStoreAdded(endpointId);
            }
        }

        public async Task RemoveEndpoint(string endpointId)
        {
            if (this.endpointSequentialStores.TryRemove(endpointId, out ISequentialStore<MessageRef> sequentialStore))
            {
                await this.storeProvider.RemoveStore(sequentialStore);
                Events.SequentialStoreRemoved(endpointId);
            }
        }

        public async Task<IMessage> Add(string endpointId, IMessage message, uint timeToLiveSecs)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            if (!this.endpointSequentialStores.TryGetValue(Preconditions.CheckNonWhiteSpace(endpointId, nameof(endpointId)), out ISequentialStore<MessageRef> sequentialStore))
            {
                throw new InvalidOperationException($"SequentialStore for endpoint {nameof(endpointId)} not found");
            }

            if (!message.SystemProperties.TryGetValue(SystemProperties.EdgeMessageId, out string edgeMessageId))
            {
                throw new InvalidOperationException("Message does not contain required system property EdgeMessageId");
            }

            TimeSpan timeToLive = timeToLiveSecs == 0 ? this.timeToLive : TimeSpan.FromSeconds(timeToLiveSecs);

            // First put the message in the entity store and then put it in the sequentialStore. This is because the pump can go fast enough that it
            // reads the message from the sequential store and tries to find the message in the entity store before the message has been added to the
            // entity store.
            // Note - if we fail to add the message to the sequential store (for some reason), then we will end up not cleaning up the message in the
            // entity store. But that should be rare enough that it might be okay. Also it is better than not being able to forward the message.
            // Alternative is to add retry logic to the pump, but that is more complicated, and could affect performance.
            // TODO - Need to support transactions for these operations. The underlying storage layers support it.
            using (MetricsV0.MessageStoreLatency(endpointId))
            {
                await this.messageEntityStore.PutOrUpdate(
                    edgeMessageId,
                    new MessageWrapper(message),
                    (m) =>
                    {
                        m.RefCount++;
                        return m;
                    });
            }

            try
            {
                using (MetricsV0.SequentialStoreLatency(endpointId))
                {
                    long offset = await sequentialStore.Append(new MessageRef(edgeMessageId, timeToLive));
                    Events.MessageAdded(offset, edgeMessageId, endpointId);
                    return new MessageWithOffset(message, offset);
                }
            }
            catch (Exception)
            {
                // If adding the message to the SequentialStore throws, then remove the message from the EntityStore as well, so that there is no leak.
                await this.messageEntityStore.Remove(edgeMessageId);
                throw;
            }
        }

        public IMessageIterator GetMessageIterator(string endpointId, long startingOffset)
        {
            if (!this.endpointSequentialStores.TryGetValue(Preconditions.CheckNonWhiteSpace(endpointId, nameof(endpointId)), out ISequentialStore<MessageRef> sequentialStore))
            {
                throw new InvalidOperationException($"Endpoint {nameof(endpointId)} not found");
            }

            // Offset starts from 0;
            startingOffset = startingOffset < DefaultStartingOffset ? DefaultStartingOffset : startingOffset;

            return new MessageIterator(this.messageEntityStore, sequentialStore, startingOffset);
        }

        public IMessageIterator GetMessageIterator(string endpointId) => this.GetMessageIterator(endpointId, DefaultStartingOffset);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.messageEntityStore?.Dispose();
                this.messagesCleaner?.Dispose();
                Events.DisposingMessageStore();
            }
        }

        /// <summary>
        /// Class that contains the message and is stored in the message store.
        /// </summary>
        internal class MessageWrapper
        {
            public MessageWrapper(IMessage message)
                : this(message, DateTime.UtcNow, 1)
            {
            }

            public MessageWrapper(IMessage message, DateTime timeStamp, int refCount)
            {
                Preconditions.CheckArgument(timeStamp != default);
                this.Message = Preconditions.CheckNotNull(message, nameof(message));
                this.TimeStamp = timeStamp;
                this.RefCount = Preconditions.CheckRange(refCount, 0, nameof(refCount));
            }

            [JsonConstructor]
            // Disabling this warning since we use this constructor on Deserialization.
            // ReSharper disable once UnusedMember.Local
            MessageWrapper(Message message, DateTime timeStamp, int refCount)
                : this((IMessage)message, timeStamp, refCount)
            {
            }

            public IMessage Message { get; }

            public DateTime TimeStamp { get; }

            public int RefCount { get; set; }
        }

        class CleanupProcessor : IDisposable
        {
            const int CleanupBatchSize = 10;
            static readonly TimeSpan CheckCleanupTaskInterval = TimeSpan.FromMinutes(30); // Check every 30 min that cleanup task is still running
            static readonly TimeSpan MinCleanupSleepTime = TimeSpan.FromSeconds(30); // Sleep for 30 secs minimum between clean up loops
            readonly MessageStore messageStore;
            readonly Timer ensureCleanupTaskTimer;
            readonly CancellationTokenSource cancellationTokenSource;
            readonly bool checkEntireQueueOnCleanup;
            readonly int messageCleanupIntervalSecs;
            readonly IMetricsCounter expiredCounter;
            Task cleanupTask;

            public CleanupProcessor(MessageStore messageStore, bool checkEntireQueueOnCleanup, int messageCleanupIntervalSecs)
            {
                this.checkEntireQueueOnCleanup = checkEntireQueueOnCleanup;
                this.messageStore = messageStore;
                this.cancellationTokenSource = new CancellationTokenSource();
                this.messageCleanupIntervalSecs = messageCleanupIntervalSecs;
                this.expiredCounter = Metrics.Instance.CreateCounter(
                   "messages_dropped",
                   "Messages cleaned up because of TTL expired",
                   new List<string> { "reason", "from", "from_route_output", MetricsConstants.MsTelemetry });
                this.ensureCleanupTaskTimer = new Timer(this.EnsureCleanupTask, null, TimeSpan.Zero, CheckCleanupTaskInterval);
                Events.CreatedCleanupProcessor(checkEntireQueueOnCleanup, messageCleanupIntervalSecs);
            }

            public void Dispose()
            {
                this.ensureCleanupTaskTimer?.Dispose();
                this.cancellationTokenSource?.Cancel();
                // wait for 30 secs for the cleanup task to finish.
                this.cleanupTask?.Wait(TimeSpan.FromSeconds(30));
                // Not disposing the cleanup task, in case it is not completed yet.
            }

            void EnsureCleanupTask(object state)
            {
                if (this.cleanupTask == null || this.cleanupTask.IsCompleted)
                {
                    this.cleanupTask = Task.Run(() => this.CleanupMessages());
                    Events.CleanupTaskInitialized();
                }
            }

            /// <summary>
            /// Messages need to be cleaned up in the following scenarios –
            /// 1.When the message expires (exceeds TTL)
            /// 2.When a message has been processed (indicated by the the checkpoint)
            /// 3.When there are 0 references to a message in the store from the message queues,
            /// the message itself is deleted from the store (this means the message was successfully delivered to all endpoints).
            /// // TODO - Update cleanup logic to cleanup expired messages from entity store as well.
            /// </summary>
            async Task CleanupMessages()
            {
                try
                {
                    await this.CleanQueue(this.checkEntireQueueOnCleanup);
                }
                catch (Exception ex)
                {
                    Events.ErrorCleaningMessages(ex);
                    throw;
                }
            }

            private async Task CleanQueue(bool checkEntireQueueOnCleanup)
            {
                long totalCleanupCount = 0;
                long totalCleanupStoreCount = 0;
                while (true)
                {
                    foreach (KeyValuePair<string, ISequentialStore<MessageRef>> endpointSequentialStore in this.messageStore.endpointSequentialStores)
                    {
                        var messageQueueId = endpointSequentialStore.Key;
                        try
                        {
                            if (this.cancellationTokenSource.IsCancellationRequested)
                            {
                                return;
                            }

                            var (endpointId, priority) = MessageQueueIdHelper.ParseMessageQueueId(messageQueueId);
                            Events.CleanupTaskStarted(messageQueueId);
                            CheckpointData checkpointData = await this.messageStore.checkpointStore.GetCheckpointDataAsync(messageQueueId, CancellationToken.None);
                            ISequentialStore<MessageRef> sequentialStore = endpointSequentialStore.Value;
                            Events.CleanupCheckpointState(messageQueueId, checkpointData);
                            int cleanupEntityStoreCount = 0;

                            // If checkEntireQueueOnCleanup is set to false, we only peek the head, message counts is tailOffset-headOffset+1
                            // otherwise count while iterating over the queue.
                            var headOffset = 0L;
                            var tailOffset = sequentialStore.GetTailOffset(CancellationToken.None);
                            var messageCount = 0L;

                            async Task<bool> DeleteMessageCallback(long offset, MessageRef messageRef)
                            {
                                var expiry = messageRef.TimeStamp + messageRef.TimeToLive;
                                if (offset > checkpointData.Offset && expiry > DateTime.UtcNow)
                                {
                                    // message is not sent and not expired, increase message counts
                                    messageCount++;
                                    return false;
                                }

                                headOffset = Math.Max(headOffset, offset);
                                bool deleteMessage = false;

                                // Decrement ref count.
                                var message = await this.messageStore.messageEntityStore.Update(
                                    messageRef.EdgeMessageId,
                                    m =>
                                    {
                                        if (m.RefCount > 0)
                                        {
                                            m.RefCount--;
                                        }

                                        if (m.RefCount == 0)
                                        {
                                            deleteMessage = true;
                                        }

                                        return m;
                                    });

                                if (deleteMessage)
                                {
                                    if (offset > checkpointData.Offset && expiry <= DateTime.UtcNow)
                                    {
                                        this.expiredCounter.Increment(1, new[] { "ttl_expiry", message?.Message.GetSenderId(), message?.Message.GetOutput(), bool.TrueString });
                                    }

                                    await this.messageStore.messageEntityStore.Remove(messageRef.EdgeMessageId);
                                    cleanupEntityStoreCount++;
                                }

                                return true;
                            }

                            // With the addition of PriorityQueues, the CleanupProcessor assumptions change slightly:
                            // Previously, we could always assume that if a message at the head of the queue should not be deleted,
                            // then none of the other messages in the queue should be either. Now, because we can have different TTL's
                            // for messages within the same queue, there can be messages that have expired in the queue after the head.
                            // The checkEntireQueueOnCleanup flag is an environment variable for edgeHub. If it is set to true, we will
                            // check the entire queue every time cleanup processor runs. If it is set to false, we just remove the oldest
                            // items in the queue until we get to one that is not expired.
                            int cleanupCount = 0;
                            if (checkEntireQueueOnCleanup)
                            {
                                IEnumerable<(long, MessageRef)> batch;
                                long offset = sequentialStore.GetHeadOffset(this.cancellationTokenSource.Token);
                                do
                                {
                                    batch = await sequentialStore.GetBatch(offset, CleanupBatchSize);
                                    foreach ((long, MessageRef) messageWithOffset in batch)
                                    {
                                        if (await sequentialStore.RemoveOffset(DeleteMessageCallback, messageWithOffset.Item1, this.cancellationTokenSource.Token))
                                        {
                                            cleanupCount++;
                                        }
                                    }

                                    offset += CleanupBatchSize;
                                }
                                while (batch.Any());
                            }
                            else
                            {
                                while (await sequentialStore.RemoveFirst(DeleteMessageCallback))
                                {
                                    cleanupCount++;
                                }

                                messageCount = tailOffset - headOffset + 1;
                            }

                            // update Metrics for message counts
                            Checkpointer.Metrics.QueueLength.Set(messageCount, new[] { endpointId, priority.ToString(), bool.TrueString });
                            totalCleanupCount += cleanupCount;
                            totalCleanupStoreCount += cleanupEntityStoreCount;
                            Events.CleanupCompleted(messageQueueId, cleanupCount, cleanupEntityStoreCount, totalCleanupCount, totalCleanupStoreCount);
                        }
                        catch (Exception ex)
                        {
                            Events.ErrorCleaningMessagesForEndpoint(ex, messageQueueId);
                        }
                    }

                    await Task.Delay(this.GetCleanupTaskSleepTime());
                }
            }

            TimeSpan GetCleanupTaskSleepTime()
            {
                // Must wait MinCleanupSleepTime, even if given interval is lower.
                return TimeSpan.FromSeconds(Math.Max(this.messageCleanupIntervalSecs, MinCleanupSleepTime.TotalSeconds));
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.MessageStore;
            static readonly ILogger Log = Logger.Factory.CreateLogger<MessageStore>();

            enum EventIds
            {
                MessageStoreCreated = IdStart,
                DisposingMessageStore,
                CleanupTaskStarted,
                ErrorCleaningMessagesForEndpoint,
                ErrorCleaningMessages,
                CleanupCompleted,
                TtlUpdated,
                SequentialStoreAdded,
                SequentialStoreRemoved,
                GettingNextBatch,
                ObtainedNextBatch,
                CleanupCheckpointState,
                MessageAdded,
                ErrorGettingMessagesBatch,
                CreatedCleanupProcessor
            }

            public static void MessageStoreCreated()
            {
                Log.LogInformation((int)EventIds.MessageStoreCreated, Invariant($"Created new message store"));
            }

            public static void DisposingMessageStore()
            {
                Log.LogInformation((int)EventIds.DisposingMessageStore, "Disposing message store");
            }

            public static void CleanupTaskStarted(string endpointId)
            {
                Log.LogInformation((int)EventIds.CleanupTaskStarted, Invariant($"Started task to cleanup processed and stale messages for endpoint {endpointId}"));
            }

            public static void CleanupTaskInitialized()
            {
                Log.LogInformation((int)EventIds.CleanupTaskStarted, "Started task to cleanup processed and stale messages");
            }

            public static void ErrorCleaningMessagesForEndpoint(Exception ex, string endpointId)
            {
                Log.LogWarning((int)EventIds.ErrorCleaningMessagesForEndpoint, ex, Invariant($"Error cleaning up messages for endpoint {endpointId}"));
            }

            public static void ErrorCleaningMessages(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorCleaningMessages, ex, "Error cleaning up messages in message store");
            }

            public static void CleanupCompleted(string endpointId, int queueMessagesCount, int storeMessagesCount, long totalQueueMessagesCount, long totalStoreMessagesCount)
            {
                Log.LogInformation((int)EventIds.CleanupCompleted, Invariant($"Cleaned up {queueMessagesCount} messages from queue for endpoint {endpointId} and {storeMessagesCount} messages from message store."));
                Log.LogDebug((int)EventIds.CleanupCompleted, Invariant($"Total messages cleaned up from queue for endpoint {endpointId} = {totalQueueMessagesCount}, and total messages cleaned up for message store = {totalStoreMessagesCount}."));
            }

            public static void ErrorGettingMessagesBatch(string entityName, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGettingMessagesBatch, ex, $"Error getting next batch for endpoint {entityName}.");
            }

            internal static void TtlUpdated(TimeSpan timeSpan)
            {
                Log.LogInformation((int)EventIds.TtlUpdated, $"Updated message store TTL to {timeSpan.TotalSeconds} seconds");
            }

            internal static void SequentialStoreAdded(string endpointId)
            {
                Log.LogDebug((int)EventIds.SequentialStoreAdded, $"Added sequential store for endpoint {endpointId}");
            }

            internal static void SequentialStoreRemoved(string endpointId)
            {
                Log.LogDebug((int)EventIds.SequentialStoreRemoved, $"Removed sequential store for endpoint {endpointId}");
            }

            internal static void MessageNotFound(string edgeMessageId)
            {
                Log.LogWarning((int)EventIds.ErrorCleaningMessagesForEndpoint, Invariant($"Unable to find message with EdgeMessageId {edgeMessageId}"));
            }

            internal static void GettingNextBatch(string entityName, long startingOffset, int batchSize)
            {
                Log.LogDebug((int)EventIds.GettingNextBatch, $"Getting next batch for endpoint {entityName} starting from {startingOffset} with batch size {batchSize}.");
            }

            internal static void ObtainedNextBatch(string entityName, long startingOffset, int count)
            {
                Log.LogDebug((int)EventIds.ObtainedNextBatch, $"Obtained next batch for endpoint {entityName} with batch size {count}. Next start offset = {startingOffset}.");
            }

            internal static void CleanupCheckpointState(string endpointId, CheckpointData checkpointData)
            {
                Log.LogDebug((int)EventIds.CleanupCheckpointState, Invariant($"Checkpoint for endpoint {endpointId} is {checkpointData.Offset}"));
            }

            internal static void MessageAdded(long offset, string edgeMessageId, string endpointId)
            {
                // Print only after every 1000th message to avoid flooding logs.
                if (offset % 1000 == 0)
                {
                    Log.LogDebug((int)EventIds.MessageAdded, Invariant($"Added message {edgeMessageId} to store for {endpointId} at offset {offset}."));
                }
            }

            internal static void CreatedCleanupProcessor(bool checkEntireQueueOnCleanup, int cleanupInterval)
            {
                if (cleanupInterval == -1)
                {
                    Log.LogDebug((int)EventIds.CreatedCleanupProcessor, $"Created cleanup processor with checkEntireQueueLength set to {checkEntireQueueOnCleanup} ");
                }
                else
                {
                    Log.LogDebug((int)EventIds.CreatedCleanupProcessor, $"Created cleanup processor with checkEntireQueueOnCleanup set to {checkEntireQueueOnCleanup} and messageCleanupIntervalSecs set to {cleanupInterval}");
                }
            }
        }

        class MessageIterator : IMessageIterator
        {
            readonly IKeyValueStore<string, MessageWrapper> entityStore;
            readonly ISequentialStore<MessageRef> endpointSequentialStore;
            long startingOffset;

            public MessageIterator(
                IKeyValueStore<string, MessageWrapper> entityStore,
                ISequentialStore<MessageRef> endpointSequentialStore,
                long startingOffset)
            {
                this.entityStore = entityStore;
                this.endpointSequentialStore = endpointSequentialStore;
                this.startingOffset = startingOffset < 0 ? 0 : startingOffset;
            }

            public async Task<IEnumerable<IMessage>> GetNext(int batchSize)
            {
                Preconditions.CheckRange(batchSize, 1, nameof(batchSize));
                var messageList = new List<IMessage>();

                try
                {
                    Events.GettingNextBatch(this.endpointSequentialStore.EntityName, this.startingOffset, batchSize);
                    // TODO - Currently, this does not iterate over a snapshot. This should work as the cleanup and reference counting is managed at
                    // application level. But need to check if creating a snapshot for iterating is needed.
                    List<(long offset, MessageRef msgRef)> batch = (await this.endpointSequentialStore.GetBatch(this.startingOffset, batchSize)).ToList();
                    if (batch.Count > 0)
                    {
                        foreach ((long offset, MessageRef msgRef) item in batch)
                        {
                            if (DateTime.UtcNow - item.msgRef.TimeStamp >= item.msgRef.TimeToLive)
                            {
                                continue;
                            }

                            Option<MessageWrapper> messageWrapper = await this.entityStore.Get(item.msgRef.EdgeMessageId);
                            if (!messageWrapper.HasValue)
                            {
                                Events.MessageNotFound(item.msgRef.EdgeMessageId);
                            }
                            else
                            {
                                messageWrapper
                                    .Map(m => new MessageWithOffset(m.Message, item.offset))
                                    .ForEach(m => messageList.Add(m));
                            }
                        }

                        this.startingOffset = batch[batch.Count - 1].offset + 1;
                    }

                    Events.ObtainedNextBatch(this.endpointSequentialStore.EntityName, this.startingOffset, messageList.Count);
                }
                catch (Exception e)
                {
                    Events.ErrorGettingMessagesBatch(this.endpointSequentialStore.EntityName, e);
                }

                return messageList;
            }
        }

        /// <summary>
        /// Class that stores references to stored messages. This is used for maintaining endpoint queues.
        /// </summary>
        class MessageRef
        {
            public MessageRef(string edgeMessageId, TimeSpan timeToLive)
                : this(edgeMessageId, DateTime.UtcNow, timeToLive)
            {
            }

            [JsonConstructor]
            public MessageRef(string edgeMessageId, DateTime timeStamp, TimeSpan timeToLive)
            {
                Preconditions.CheckArgument(timeStamp != default(DateTime));
                this.EdgeMessageId = Preconditions.CheckNonWhiteSpace(edgeMessageId, nameof(edgeMessageId));
                this.TimeStamp = timeStamp;
                this.TimeToLive = timeToLive;
            }

            public string EdgeMessageId { get; }

            public DateTime TimeStamp { get; }

            public TimeSpan TimeToLive { get; }
        }

        // Wrapper to allow adding offset to an existing IMessage object
        class MessageWithOffset : IMessage
        {
            readonly IMessage inner;

            public MessageWithOffset(IMessage message, long offset)
            {
                this.inner = Preconditions.CheckNotNull(message, nameof(message));
                this.Offset = Preconditions.CheckRange(offset, 0, nameof(offset));
            }

            public void Dispose() => this.inner.Dispose();

            public IMessageSource MessageSource => this.inner.MessageSource;

            public byte[] Body => this.inner.Body;

            public IReadOnlyDictionary<string, string> Properties => this.inner.Properties;

            public IReadOnlyDictionary<string, string> SystemProperties => this.inner.SystemProperties;

            public long Offset { get; }

            public uint ProcessedPriority { get; set; }

            public DateTime EnqueuedTime => this.inner.EnqueuedTime;

            public DateTime DequeuedTime => this.inner.DequeuedTime;

            public QueryValue GetQueryValue(string queryString) => this.inner.GetQueryValue(queryString);

            public long Size() => this.inner.Size();
        }

        static class MetricsV0
        {
            static readonly TimerOptions MessageEntityStorePutOrUpdateLatencyOptions = new TimerOptions
            {
                Name = "MessageEntityStorePutOrUpdateLatencyMs",
                MeasurementUnit = Unit.None,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds
            };

            static readonly TimerOptions SequentialStoreAppendLatencyOptions = new TimerOptions
            {
                Name = "SequentialStoreAppendLatencyMs",
                MeasurementUnit = Unit.None,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds
            };

            public static IDisposable MessageStoreLatency(string identity) => Util.Metrics.MetricsV0.Latency(GetTags(identity), MessageEntityStorePutOrUpdateLatencyOptions);

            public static IDisposable SequentialStoreLatency(string identity) => Util.Metrics.MetricsV0.Latency(GetTags(identity), SequentialStoreAppendLatencyOptions);

            internal static MetricTags GetTags(string id)
            {
                return new MetricTags("EndpointId", id);
            }
        }
    }
}
