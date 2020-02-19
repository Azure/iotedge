// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides functionality to store messages
    /// Assumes that each message has a SystemProperty EdgeMessageId that
    /// uniquely identifies a given message
    /// Internally implement 2 types of stores (tables) -
    ///     1. A message store containing messages
    ///     2. A message queue for each endpoint referencing messages in the message store
    /// </summary>
    public interface IMessageStore : IDisposable
    {
        /// <summary>
        /// TODO: 6099894 - Update StoringAsyncEndpointExecutor message enqueue logic to be aware of priorities
        /// Remove this overload once the executor is enqueuing/dequeuing messages by priority
        /// </summary>
        Task<IMessage> Add(string endpointId, IMessage message);

        /// <summary>
        /// Creates an entry in the message queue for the given endpoint
        /// and returns the offset of that entry. If an entry already
        /// exists for the given message ID, then it's updated.
        /// </summary>
        Task<IMessage> Add(string endpointId, IMessage message, uint priority);

        /// <summary>
        /// TODO: 6099894 - Update StoringAsyncEndpointExecutor message enqueue logic to be aware of priorities
        /// Remove this overload once the executor is enqueuing/dequeuing messages by priority
        /// </summary>
        IMessageIterator GetMessageIterator(string endpointId, long startingOffset);

        /// <summary>
        /// Returns an iterator that allows reading messages starting from the given offset.
        /// </summary>
        IMessageIterator GetMessageIterator(string endpointId, uint priority, long startingOffset);

        /// <summary>
        /// Returns an iterator that allows reading messages starting from the first message.
        /// </summary>
        IMessageIterator GetMessageIterator(string endpointId);

        /// <summary>
        /// TODO: 6099894 - Update StoringAsyncEndpointExecutor message enqueue logic to be aware of priorities
        /// Remove this overload once the executor is enqueuing/dequeuing messages by priority
        /// </summary>
        Task AddEndpoint(string endpointId);

        /// <summary>
        /// Adds a new queue for endpoint with the given priority to the store.
        /// </summary>
        Task AddEndpointQueue(string endpointId, uint priority);

        /// <summary>
        /// TODO: 6099894 - Update StoringAsyncEndpointExecutor message enqueue logic to be aware of priorities
        /// Remove this overload once the executor is enqueuing/dequeuing messages by priority
        /// </summary>
        Task RemoveEndpoint(string endpointId);

        /// <summary>
        /// Removes the queue for the given endpoint and priority from the store.
        /// </summary>
        Task RemoveEndpointQueue(string endpointId, uint priority);

        /// <summary>
        /// Set the expiry time for messages in the store
        /// </summary>
        void SetTimeToLive(TimeSpan timeToLive);
    }
}
