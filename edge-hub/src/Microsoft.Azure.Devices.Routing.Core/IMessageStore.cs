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
        /// Finds message with same edgeMessageId in the message store,
        /// and if not found, creates one.
        /// Creates an entry in the message queue for the given endpoint
        /// and returns the offset of that entry.
        /// </summary>
        Task<IMessage> Add(string endpointId, IMessage message);

        /// <summary>
        /// Returns an iterator that allows reading messages starting from the given offset.
        /// </summary>
        IMessageIterator GetMessageIterator(string endpointId, long startingOffset);

        /// <summary>
        /// Returns an iterator that allows reading messages starting from the first message.
        /// </summary>
        IMessageIterator GetMessageIterator(string endpointId);

        /// <summary>
        /// Adds a new endpoint to the store
        /// </summary>
        Task AddEndpoint(string endpointId);

        /// <summary>
        /// Removes the endpoint from the store
        /// </summary>
        Task RemoveEndpoint(string endpointId);

        /// <summary>
        /// Set the expiry time for messages in the store
        /// </summary>
        void SetTimeToLive(TimeSpan timeToLive);
    }
}
