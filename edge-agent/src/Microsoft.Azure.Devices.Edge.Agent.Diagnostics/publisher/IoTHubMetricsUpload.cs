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
        readonly IKeyValueStore<Guid, (byte[] data, int retry)> dataStore;

        Task retryTask = null;

        public IoTHubMetricsUpload(IEdgeAgentConnection edgeAgentConnection, IStoreProvider storeProvider)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            this.dataStore = Preconditions.CheckNotNull(storeProvider.GetEntityStore<Guid, (byte[], int)>("Diagnostic Messages"), "dataStore");
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));
            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();

            // TODO: add check for too big of a message
            if (data.Length > 0)
            {
                await this.SendMessage(data);
            }
        }

        async Task<bool> SendMessage(byte[] data, int retryNum = 0)
        {
            Message message = this.BuildMessage(data);
            try
            {
                await this.edgeAgentConnection.SendEventAsync(message);
            }
            catch (Exception ex)
            {
                if (ex.HasTimeoutException() && retryNum <= MaxRetries)
                {
                    await this.dataStore.Put(Guid.NewGuid(), (data, retryNum + 1));
                    this.StartRetrying();
                    return false;
                }
            }

            return true;
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
            await this.dataStore.IterateBatch(1000, async (guid, d) =>
            {
                if (!await this.SendMessage(d.data, d.retry))
                {
                    allCompleatedSuccesfully = false;
                }

                await this.dataStore.Remove(guid);
            });

            return allCompleatedSuccesfully;
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
