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
    using Microsoft.Azure.Devices.Edge.Util.Retrying;
    using Microsoft.Extensions.Logging.Abstractions;

    public sealed class IoTHubMetricsUpload : IMetricsPublisher, IDisposable
    {
        readonly IEdgeAgentConnection edgeAgentConnection;
        readonly RetryWithBackoff<byte[], Option<Exception>> retryHandler;

        public IoTHubMetricsUpload(IEdgeAgentConnection edgeAgentConnection, IStoreProvider storeProvider)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));

            IBackoff backoff = new Util.Retrying.ExponentialBackoff(TimeSpan.FromMinutes(1), 1.5, TimeSpan.FromMinutes(30));
            this.retryHandler = new RetryWithBackoff<byte[], Option<Exception>>(this.SendMessage, this.ShouldRetry, backoff, new Dictionary<Guid, byte[]>());
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));
            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();

            // TODO: add check for too big of a message
            if (data.Length > 0)
            {
                await this.retryHandler.DoWorkAsync(data, cancellationToken);
            }
        }

        /// <summary>
        /// Sends the given bytes to IoT Hub.
        /// </summary>
        /// <param name="data">Metrics in binary form.</param>
        /// <returns>Option containing possible exception.</returns>
        async Task<Option<Exception>> SendMessage(byte[] data, CancellationToken cancellationToken)
        {
            Message message = this.BuildMessage(data);
            try
            {
                await this.edgeAgentConnection.SendEventAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                return Option.Some(ex);
            }

            // no retry
            return Option.None<Exception>();
        }

        bool ShouldRetry(Option<Exception> result)
        {
            return result.Exists(ex => ex.HasTimeoutException());
        }

        Message BuildMessage(byte[] data)
        {
            Message message = new Message(data);
            message.ContentType = "application/x-azureiot-edgeruntimediagnostics";

            return message;
        }

        public void Dispose()
        {
            this.retryHandler.Dispose();
        }
    }
}
