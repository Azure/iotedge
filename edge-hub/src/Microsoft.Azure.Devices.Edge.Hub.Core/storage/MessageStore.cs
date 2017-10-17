// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static System.FormattableString;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;

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

        public MessageStore(IStoreProvider storeProvider, ICheckpointStore checkpointStore, TimeSpan timeToLive)
        {
            this.storeProvider = Preconditions.CheckNotNull(storeProvider);
            this.messageEntityStore = this.storeProvider.GetEntityStore<string, MessageWrapper>(Constants.MessageStorePartitionKey);
            this.endpointSequentialStores = new ConcurrentDictionary<string, ISequentialStore<MessageRef>>();
            this.timeToLive = timeToLive;
            this.checkpointStore = Preconditions.CheckNotNull(checkpointStore, nameof(checkpointStore));
            this.messagesCleaner = new CleanupProcessor(this);
            Events.MessageStoreCreated(this);
        }

        public void SetTimeToLive(TimeSpan timeSpan)
        {
            this.timeToLive = timeSpan;
        }

        public async Task AddEndpoint(string endpointId)
        {
            ISequentialStore<MessageRef> sequentialStore = await this.storeProvider.GetSequentialStore<MessageRef>(endpointId);
            this.endpointSequentialStores.TryAdd(endpointId, sequentialStore);
        }

        public Task RemoveEndpoint(string endpointId)
        {
            if (this.endpointSequentialStores.TryGetValue(endpointId, out ISequentialStore<MessageRef> sequentialStore))
            {
                this.storeProvider.RemoveStore(sequentialStore);
            }
            return Task.CompletedTask;
        }

        public async Task<long> Add(string endpointId, IMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            if (!this.endpointSequentialStores.TryGetValue(Preconditions.CheckNonWhiteSpace(endpointId, nameof(endpointId)), out ISequentialStore<MessageRef> sequentialStore))
            {
                throw new InvalidOperationException($"SequentialStore for endpoint {nameof(endpointId)} not found");
            }

            if (!message.SystemProperties.TryGetValue(Core.SystemProperties.EdgeMessageId, out string edgeMessageId))
            {
                throw new InvalidOperationException("Message does not contain required system property EdgeMessageId");
            }

            long offset = await sequentialStore.Append(new MessageRef(edgeMessageId));
            await this.messageEntityStore.PutOrUpdate(edgeMessageId, new MessageWrapper(message), (m) =>
            {
                m.RefCount++;
                return m;
            });
            return offset;
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

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.messageEntityStore?.Dispose();
                this.messagesCleaner?.Dispose();
                Events.DisposingMessageStore();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        class MessageIterator : IMessageIterator
        {
            long startingOffset;
            readonly IKeyValueStore<string, MessageWrapper> entityStore;
            readonly ISequentialStore<MessageRef> endpointSequentialStore;

            public MessageIterator(IKeyValueStore<string, MessageWrapper> entityStore,
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

                // TODO - Currently, this does not iterate over a snapshot. This should work as the cleanup and reference counting is managed at 
                // application level. But need to check if creating a snapshot for iterating is needed.
                List<(long offset, MessageRef msgRef)> batch = (await this.endpointSequentialStore.GetBatch(this.startingOffset, batchSize))
                    .ToList();
                if (batch.Count > 0)
                {
                    foreach ((long offset, MessageRef msgRef) item in batch)
                    {
                        Option<MessageWrapper> messageWrapper = await this.entityStore.Get(item.msgRef.EdgeMessageId);
                        IMessage message = messageWrapper.Match(
                            m => this.AddMessageOffset(m.Message, item.offset),
                            () => throw new InvalidOperationException($"Unable to find message with EdgeMessageId {item.msgRef.EdgeMessageId}"));
                        messageList.Add(message);
                    }

                    this.startingOffset = batch[batch.Count - 1].offset + 1;
                }

                return messageList;
            }

            IMessage AddMessageOffset(IMessage message, long offset)
            {
                return new Message(
                    message.MessageSource,
                    message.Body,
                    message.Properties.ToDictionary(),
                    message.SystemProperties.ToDictionary(),
                    offset,
                    message.EnqueuedTime,
                    message.DequeuedTime);
            }
        }

        /// <summary>
        /// Class that contains the message and is stored in the messa
        /// </summary>
        class MessageWrapper
        {
            public MessageWrapper(IMessage message)
                : this(message, DateTime.UtcNow, 1)
            {
            }

            [JsonConstructor]
            public MessageWrapper(IMessage message, DateTime timeStamp, int refCount)
            {
                Preconditions.CheckArgument(timeStamp != default(DateTime));
                this.Message = Preconditions.CheckNotNull(message, nameof(message));
                this.TimeStamp = timeStamp;
                this.RefCount = Preconditions.CheckRange(refCount, 0, nameof(refCount));
            }

            public IMessage Message { get; }

            public DateTime TimeStamp { get; }

            public int RefCount { get; set; }
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

        class CleanupProcessor : IDisposable
        {
            const int EnsureCleanupTaskTimerSecs = 600; // Run once every 10 mins.
            const int MinCleanupSleepTimeSecs = 30; // Sleep for 30 secs
            readonly MessageStore messageStore;
            readonly Timer ensureCleanupTaskTimer;
            readonly CancellationTokenSource cancellationTokenSource;
            Task cleanupTask;

            public CleanupProcessor(MessageStore messageStore)
            {
                this.messageStore = messageStore;
                this.ensureCleanupTaskTimer = new Timer(this.EnsureCleanupTask, null, TimeSpan.Zero, TimeSpan.FromSeconds(EnsureCleanupTaskTimerSecs));
                this.cancellationTokenSource = new CancellationTokenSource();
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
            /// 1.	When the message expires (exceeds TTL)
            /// 2.	When a message has been processed (indicated by the the checkpoint)
            /// 3.	When there are 0 references to a message in the store from the message queues, 
            /// the message itself is deleted from the store (this means the message was successfully delivered to all endpoints).
            /// </summary>
            async Task CleanupMessages()
            {
                try
                {
                    double cleanupTaskSleepTimeSecs = Math.Min(this.messageStore.timeToLive.TotalSeconds / 2, EnsureCleanupTaskTimerSecs);
                    TimeSpan cleanupTaskSleepTime = TimeSpan.FromSeconds(cleanupTaskSleepTimeSecs);
                    while (true)
                    {
                        TimeSpan sleepTime = TimeSpan.FromSeconds(MinCleanupSleepTimeSecs);
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
                                    }

                                    return true;
                                }

                                int cleanupCount = 0;
                                while (await sequentialStore.RemoveFirst(DeleteMessageCallback))
                                {
                                    cleanupCount++;
                                }

                                Events.CleanupCompleted(endpointSequentialStore.Key, cleanupCount);
                                await Task.Delay(sleepTime, this.cancellationTokenSource.Token);
                            }
                            catch (Exception ex)
                            {
                                Events.ErrorCleaningMessagesForEndpoint(ex, endpointSequentialStore.Key);
                            }
                        }
                        await Task.Delay(cleanupTaskSleepTime);
                    }
                }
                catch (Exception ex)
                {
                    Events.ErrorCleaningMessages(ex);
                    throw;
                }
            }

            public void Dispose()
            {
                this.ensureCleanupTaskTimer?.Dispose();
                this.cancellationTokenSource?.Cancel();
                // wait for 30 secs for the cleanup task to finish.
                this.cleanupTask?.Wait(TimeSpan.FromSeconds(30));
                // Not disposing the cleanup task, in case it is not completed yet. 
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<MessageStore>();
            const int IdStart = HubCoreEventIds.MessageStore;

            enum EventIds
            {
                MessageStoreCreated = IdStart,
                DisposingMessageStore,
                CleanupTaskStarted,
                ErrorCleaningMessagesForEndpoint,
                ErrorCleaningMessages,
                CleanupCompleted
            }

            public static void MessageStoreCreated(MessageStore messageStore)
            {
                Log.LogInformation((int)EventIds.MessageStoreCreated, Invariant($"Created new message store for {messageStore.endpointSequentialStores.Count} endpoints"));
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
                Log.LogError((int)EventIds.ErrorCleaningMessages, ex, "Error cleaning up messages in message store");
            }

            public static void CleanupCompleted(string endpointId, int count)
            {
                Log.LogInformation((int)EventIds.CleanupCompleted, Invariant($"Cleaned up {count} messages from queue for endpoint {endpointId}"));
            }
        }
    }
}
