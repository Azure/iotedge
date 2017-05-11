// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public interface ICheckpointer : IDisposable
    {
        /// <summary>
        /// Checkpointer unique identifier
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Exposes the latest message that has been set. This is public
        /// for the time being because the Source needs to now where in
        /// the message to start reading messages when the router is created.
        /// </summary>
        long Offset { get; }

        /// <summary>
        /// Exposes the last time an endpoint failed to revive (or the first time the endpoint dies)
        /// </summary>
        Option<DateTime> LastFailedRevivalTime { get; }

        /// <summary>
        /// Exposes the first time an endpoint started failing prior to being dead (i.e. the time an endpoint started the process of dying)
        /// </summary>
        Option<DateTime> UnhealthySince { get; }

        /// <summary>
        /// Exposes the latest offset that has been proposed.
        /// </summary>
        long Proposed { get; }

        /// <summary>
        /// Checkpointer has outstanding messages. Messages have been received
        /// for processing but not yet checkpointed.
        /// </summary>
        bool HasOutstanding { get; }

        /// <summary>
        /// Called before processing on a message begins. This is to allow tracking
        /// of outstanding messages for a checkpointer.
        /// </summary>
        /// <param name="message"></param>
        void Propose(IMessage message);

        /// <summary>
        /// Checks that a messages has not already been processed.
        /// </summary>
        /// <param name="message">The message to check</param>
        /// <returns>True if it is safe to process the message. False if the message has already been processed</returns>
        bool Admit(IMessage message);

        /// <summary>
        /// <param name="successful"></param>
        /// <param name="remaining"></param>
        /// <param name="lastFailedRevivalTime"></param>
        /// <param name="token"></param>
        /// Allows checkpointing an intermediate set of messages.
        /// This is to support the case where a batch of messages is processed by the endpoint
        /// adapter, but only a portion of the messages in the batch succeed. The successful
        /// messages can be checkpointed. In order to do this efficiently and correctly
        /// the checkpointer must know about the messages in the batch that are still remaining
        /// to be processed.
        /// </summary>
        /// <returns>A task that is completed when the checkpoints have been successfully committed</returns>
        /// <returns></returns>
        Task CommitAsync(ICollection<IMessage> successful, ICollection<IMessage> remaining, Option<DateTime> lastFailedRevivalTime, Option<DateTime> unhealthySince, CancellationToken token);

        /// <summary>
        /// Closes the checkpointer and commits any outstanding messages. This must be called before disposing.
        /// </summary>
        /// <returns>A task that is completed when the checkpointer has successfully closed.</returns>
        Task CloseAsync(CancellationToken token);
    }
}