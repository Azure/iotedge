// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
        /// <param name="endpointId">Endpoint Id</param>
        /// <param name="message">Message</param>
        /// <returns>Task with offset of that entry</returns>
        Task<long> Add(string endpointId, IMessage message);

        /// <summary>
        /// Adds a new endpoint to the store
        /// </summary>
        /// <param name="endpointId">Endpoint Id</param>
        /// <returns>Task</returns>
        Task AddEndpoint(string endpointId);

        /// <summary>
        /// Returns an iterator that allows reading messages starting from the given offset.
        /// </summary>
        /// <param name="endpointId">Endpoint Id</param>
        /// <param name="startingOffset">Starting offset</param>
        /// <returns>Message iterator</returns>
        IMessageIterator GetMessageIterator(string endpointId, long startingOffset);

        /// <summary>
        /// Returns an iterator that allows reading messages starting from the first message.
        /// </summary>
        /// <param name="endpointId">Endpoint Id</param>
        /// <returns>Message iterator</returns>
        IMessageIterator GetMessageIterator(string endpointId);

        /// <summary>
        /// Removes the endpoint from the store
        /// </summary>
        /// <param name="endpointId">Endpoint Id</param>
        /// <returns>Task</returns>
        Task RemoveEndpoint(string endpointId);

        /// <summary>
        /// Set the expiry time for messages in the store
        /// </summary>
        /// <param name="timeToLive">Time to live</param>
        void SetTimeToLive(TimeSpan timeToLive);
    }
}
