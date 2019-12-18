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
        readonly long messageCount = 0;
        TimeSpan timeToLive;

        public MessageStore(IStoreProvider storeProvider, ICheckpointStore checkpointStore, TimeSpan timeToLive)
        {
            this.storeProvider = Preconditions.CheckNotNull(storeProvider);
            this.messageEntityStore = this.storeProvider.GetEntityStore<string, MessageWrapper>(Constants.MessageStorePartitionKey);
            this.endpointSequentialStores = new ConcurrentDictionary<string, ISequentialStore<MessageRef>>();
            this.timeToLive = timeToLive;
            this.checkpointStore = Preconditions.CheckNotNull(checkpointStore, nameof(checkpointStore));
            this.messagesCleaner = new CleanupProcessor(this);
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

        public async Task<IMessage> Add(string endpointId, IMessage message)
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
                    long offset = await sequentialStore.Append(new MessageRef(edgeMessageId));
                    Events.MessageAdded(offset, edgeMessageId, endpointId, this.messageCount);
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
        /// Class that contains the message and is stored in the messa
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
            static readonly TimeSpan CleanupTaskFrequency = TimeSpan.FromMinutes(30); // Run once every 30 mins.
            static readonly TimeSpan MinCleanupSleepTime = TimeSpan.FromSeconds(30); // Sleep for 30 secs
            readonly MessageStore messageStore;
            readonly Timer ensureCleanupTaskTimer;
            readonly CancellationTokenSource cancellationTokenSource;
            Task cleanupTask;

            public CleanupProcessor(MessageStore messageStore)
            {
                this.messageStore = messageStore;
                this.ensureCleanupTaskTimer = new Timer(this.EnsureCleanupTask, null, TimeSpan.Zero, CleanupTaskFrequency);
                this.cancellationTokenSource = new CancellationTokenSource();
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
                long totalCleanupCount = 0;
                long totalCleanupStoreCount = 0;

                try
                {
                    while (true)
                    {
                        foreach (KeyValuePair<string, ISequentialStore<MessageRef>> endpointSequentialStore in this.messageStore.endpointSequentialStores)
                        {
                            try
                            {
                                if (this.cancellationTokenSource.IsCancellationRequested)
                                {
                                    return;
                                }

                                Events.CleanupTaskStarted(endpointSequentialStore.Key);
                                CheckpointData checkpointData = await this.messageStore.checkpointStore.GetCheckpointDataAsync(endpointSequentialStore.Key, CancellationToken.None);
                                ISequentialStore<MessageRef> sequentialStore = endpointSequentialStore.Value;
                                Events.CleanupCheckpointState(endpointSequentialStore.Key, checkpointData);
                                int cleanupEntityStoreCount = 0;

                                async Task<bool> DeleteMessageCallback(long offset, MessageRef messageRef)
                                {
                                    if (checkpointData.Offset < offset &&
                                        DateTime.UtcNow - messageRef.TimeStamp < this.messageStore.timeToLive)
                                    {
                                        return false;
                                    }

                                    bool deleteMessage = false;

                                    // Decrement ref count.
                                    await this.messageStore.messageEntityStore.Update(
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
                                        await this.messageStore.messageEntityStore.Remove(messageRef.EdgeMessageId);
                                        cleanupEntityStoreCount++;
                                    }

                                    return true;
                                }

                                int cleanupCount = 0;
                                while (await sequentialStore.RemoveFirst(DeleteMessageCallback))
                                {
                                    cleanupCount++;
                                }

                                totalCleanupCount += cleanupCount;
                                totalCleanupStoreCount += cleanupEntityStoreCount;
                                Events.CleanupCompleted(endpointSequentialStore.Key, cleanupCount, cleanupEntityStoreCount, totalCleanupCount, totalCleanupStoreCount);
                                await Task.Delay(MinCleanupSleepTime, this.cancellationTokenSource.Token);
                            }
                            catch (Exception ex)
                            {
                                Events.ErrorCleaningMessagesForEndpoint(ex, endpointSequentialStore.Key);
                            }
                        }

                        await Task.Delay(this.GetCleanupTaskSleepTime());
                    }
                }
                catch (Exception ex)
                {
                    Events.ErrorCleaningMessages(ex);
                    throw;
                }
            }

            TimeSpan GetCleanupTaskSleepTime() => this.messageStore.timeToLive.TotalSeconds / 2 < CleanupTaskFrequency.TotalSeconds
                ? TimeSpan.FromSeconds(this.messageStore.timeToLive.TotalSeconds / 2)
                : CleanupTaskFrequency;
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
                ErrorGettingMessagesBatch
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

            internal static void MessageAdded(long offset, string edgeMessageId, string endpointId, long messageCount)
            {
                // Print only after every 1000th message to avoid flooding logs.
                if (offset % 1000 == 0)
                {
                    Log.LogDebug((int)EventIds.MessageAdded, Invariant($"Added message {edgeMessageId} to store for {endpointId} at offset {offset} - messageCount = {messageCount}"));
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
                    List<(long offset, MessageRef msgRef)> batch = (await this.endpointSequentialStore.GetBatch(this.startingOffset, batchSize))
                        .ToList();
                    if (batch.Count > 0)
                    {
                        foreach ((long offset, MessageRef msgRef) item in batch)
                        {
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
        /// Class that stores references to stored messages. This is used for maintaining endpoint queues
        /// </summary>
        class MessageRef
        {
            public MessageRef(string edgeMessageId)
                : this(edgeMessageId, DateTime.UtcNow)
            {
            }

            [JsonConstructor]
            public MessageRef(string edgeMessageId, DateTime timeStamp)
            {
                Preconditions.CheckArgument(timeStamp != default(DateTime));
                this.EdgeMessageId = Preconditions.CheckNonWhiteSpace(edgeMessageId, nameof(edgeMessageId));
                this.TimeStamp = timeStamp;
            }

            public string EdgeMessageId { get; }

            public DateTime TimeStamp { get; }
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
