// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.WebJobs;

    /// <summary>
    /// Core object to send events to EdgeHub. 
    /// Any user parameter that sends EdgeHub events will eventually get bound to this object. 
    /// This will queue events and send in batches, also keeping under the 256kb edge hub limit per batch. 
    /// </summary>
    public class EdgeHubAsyncCollector : IAsyncCollector<Message>
    {
        readonly DeviceClient client;
        readonly EdgeHubAttribute attribute;
        readonly int batchSize;

        const int DefaultBatchSize = 10;
        // Max batch size limit from IoTHub
        const int MaxBatchSize = 500;
        // Suggested to use 240k instead of 256k to leave padding room for headers.
        const int MaxByteSize = 240 * 1024;

        List<Message> list = new List<Message>();
        // total size of bytes in list that we'll be sending in this batch. 
        int currentByteSize = 0;

        /// <summary>
        /// Create a sender around the given client. 
        /// </summary>
        /// <param name="client"></param>
        public EdgeHubAsyncCollector(DeviceClient client, EdgeHubAttribute attribute)
        {
            this.client = client;
            this.attribute = attribute;
            this.batchSize = attribute.BatchSize > 0 ? (attribute.BatchSize > MaxBatchSize ? MaxBatchSize : attribute.BatchSize) : DefaultBatchSize;
        }

        /// <summary>
        /// Add an event. 
        /// </summary>
        /// <param name="item">The event to add</param>
        /// <param name="cancellationToken">a cancellation token. </param>
        /// <returns></returns>
        public async Task AddAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte[] payload = message.GetBytes();
            Message copy = Utils.GetMessageCopy(payload, message);
            IList<Message> batch = null;

            lock (this.list)
            {
                var size = payload.Length;
                if (size > MaxByteSize)
                {
                    // Single event is too large to add.
                    string msg = string.Format("Event is too large. Event is approximately {0}b and max size is {1}b", size, MaxByteSize);
                    throw new InvalidOperationException(msg);
                }

                if (this.currentByteSize + size > MaxByteSize)
                {
                    batch = this.TakeSnapshot();
                }

                this.list.Add(copy);
                this.currentByteSize += size;

                if (this.list.Count == this.batchSize && batch == null)
                {
                    batch = TakeSnapshot();
                }
            }

            await this.SendBatchAsync(batch);
        }

        /// <summary>
        /// synchronously flush events that have been queued up via AddAsync.
        /// </summary>
        /// <param name="cancellationToken">a cancellation token</param>
        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            IList<Message> batch = this.TakeSnapshot();
            await this.SendBatchAsync(batch);
        }

        /// <summary>
        /// Send the batch of events.
        /// </summary>
        /// <param name="batch">the set of events to send</param>
        protected virtual async Task SendBatchAsync(IList<Message> batch)
        {
            if (batch == null || batch.Count == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(this.attribute.OutputName))
            {
                await this.client.SendEventBatchAsync(batch);
            }
            else
            {
                await this.client.SendEventBatchAsync(this.attribute.OutputName, batch);
            }
        }

        IList<Message> TakeSnapshot()
        {
            IList<Message> batch = null;
            lock (this.list)
            {
                batch = this.list.ToList();
                this.list.Clear();
                this.currentByteSize = 0;
            }
            return batch;
        }
    }
}