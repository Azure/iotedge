// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Newtonsoft.Json;
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
        readonly IEntityStore<string, MessageWrapper> messageStore;
        readonly IDictionary<string, ISequentialStore<MessageRef>> endpointSequentialStores;

        MessageStore(IEntityStore<string, MessageWrapper> entityStore, IDictionary<string, ISequentialStore<MessageRef>> endpointSequentialStores)
        {
            this.messageStore = Preconditions.CheckNotNull(entityStore, nameof(entityStore));
            this.endpointSequentialStores = Preconditions.CheckNotNull(endpointSequentialStores, nameof(endpointSequentialStores));
        }

        public static async Task<IMessageStore> CreateAsync(IStoreProvider storeProvider, IEnumerable<string> endpoints)
        {
            Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
            Preconditions.CheckNotNull(endpoints, nameof(endpoints));

            IEntityStore<string, MessageWrapper> entityStore = storeProvider.GetEntityStore<string, MessageWrapper>(Constants.MessageStorePartitionKey);
            var endpointSequentialStores = new Dictionary<string, ISequentialStore<MessageRef>>();
            foreach (string endpoint in endpoints)
            {
                ISequentialStore<MessageRef> sequentialStore = await storeProvider.GetSequentialStore<MessageRef>(endpoint);
                endpointSequentialStores.Add(endpoint, sequentialStore);
            }
            var messageStore = new MessageStore(entityStore, new ConcurrentDictionary<string, ISequentialStore<MessageRef>>(endpointSequentialStores));
            return messageStore;
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
                throw new InvalidOperationException($"Message does not contain required system property EdgeMessageId");
            }

            long offset = await sequentialStore.Add(new MessageRef(edgeMessageId));
            await this.messageStore.PutOrUpdate(edgeMessageId, new MessageWrapper(message), (m) =>
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

            return new MessageIterator(this.messageStore, sequentialStore, startingOffset);
        }

        public IMessageIterator GetMessageIterator(string endpointId) => this.GetMessageIterator(endpointId, DefaultStartingOffset);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.messageStore?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
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
                this.RefCount = Preconditions.CheckRange(refCount, 1, nameof(refCount));
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
    }
}
