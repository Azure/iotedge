// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging.Abstractions;

    public sealed class IoTHubMetricsUpload : IMetricsPublisher
    {
        const int MaxRetries = 20;
        readonly IEdgeAgentConnection edgeAgentConnection;
        readonly IKeyValueStore<Guid, byte[]> messagesToRetry;

        Task retryTask = null;
        Dictionary<Guid, int> retryTracker = new Dictionary<Guid, int>();

        public IoTHubMetricsUpload(IEdgeAgentConnection edgeAgentConnection, IStoreProvider storeProvider)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            this.messagesToRetry = Preconditions.CheckNotNull(storeProvider.GetEntityStore<Guid, byte[]>("Diagnostic Messages"), "dataStore");
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));
            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();

            // TODO: add check for too big of a message
            if (data.Length > 0)
            {
                if (await this.SendMessage(data))
                {
                    await this.messagesToRetry.Put(Guid.NewGuid(), data);
                    this.StartRetrying();
                }
            }
        }

        /// <summary>
        /// Sends the given bytes to IoT Hub.
        /// </summary>
        /// <param name="data">Metrics in binary form.</param>
        /// <returns>Whether the message should be retryed.</returns>
        async Task<bool> SendMessage(byte[] data)
        {
            Message message = this.BuildMessage(data);
            try
            {
                await this.edgeAgentConnection.SendEventAsync(message);
            }
            catch (Exception ex)
            {
                // should retry
                return ex.HasTimeoutException();
            }

            // no retry
            return false;
        }

        Message BuildMessage(byte[] data)
        {
            Message message = new Message(data);
            message.ContentType = "application/x-azureiot-edgeruntimediagnostics";

            return message;
        }

        void StartRetrying()
        {
            if (this.retryTask == null)
            {
                this.retryTask = Task.Run(async () =>
                {
                    foreach (TimeSpan sleepTime in this.Backoff())
                    {
                        await Task.Delay(sleepTime);
                        if (await this.Retry())
                        {
                            break;
                        }
                    }

                    this.retryTask = null;
                });
            }
        }

        async Task<bool> Retry()
        {
            bool allCompleatedSuccesfully = true;
            await this.messagesToRetry.IterateBatch(1000, async (guid, data) =>
            {
                if (await this.SendMessage(data))
                {
                    // Message not sent.
                    allCompleatedSuccesfully = false;

                    // If max retrys hit, remove from list
                    if (!this.ShouldRetry(guid))
                    {
                        await this.messagesToRetry.Remove(guid);
                    }
                }
                else
                {
                    // Successfully sent message.
                    await this.messagesToRetry.Remove(guid);
                    this.retryTracker.Remove(guid);
                }
            });

            return allCompleatedSuccesfully;
        }

        bool ShouldRetry(Guid guid)
        {
            if (this.retryTracker.TryGetValue(guid, out int numRetrys))
            {
                if (numRetrys > MaxRetries)
                {
                    this.retryTracker.Remove(guid);
                    return false;
                }
                else
                {
                    this.retryTracker[guid] = numRetrys + 1;
                }
            }
            else
            {
                this.retryTracker[guid] = 1;
            }

            return true;
        }

        IEnumerable<TimeSpan> Backoff()
        {
            yield return TimeSpan.FromMinutes(5);
            yield return TimeSpan.FromMinutes(5);
            yield return TimeSpan.FromMinutes(10);
            yield return TimeSpan.FromMinutes(15);

            while (true)
            {
                yield return TimeSpan.FromMinutes(30);
            }
        }
    }
}
