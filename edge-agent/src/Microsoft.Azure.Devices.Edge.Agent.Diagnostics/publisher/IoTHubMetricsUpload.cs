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

    public sealed class IoTHubMetricsUpload : IMetricsPublisher, IDisposable
    {
        readonly IEdgeAgentConnection edgeAgentConnection;
        readonly RetryHandler<byte[]> retryHandler;

        public IoTHubMetricsUpload(IEdgeAgentConnection edgeAgentConnection, IStoreProvider storeProvider)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            this.retryHandler = new RetryHandler<byte[]>(this.SendMessage, storeProvider, "Diagnostic Messages");
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));
            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();

            // TODO: add check for too big of a message
            if (data.Length > 0)
            {
                await this.retryHandler.Send(data);
            }
        }

        /// <summary>
        /// Sends the given bytes to IoT Hub.
        /// </summary>
        /// <param name="data">Metrics in binary form.</param>
        /// <returns>Whether the message should be retried.</returns>
        async Task<bool> SendMessage(byte[] data, CancellationToken cancellationToken)
        {
            Message message = this.BuildMessage(data);
            try
            {
                await this.edgeAgentConnection.SendEventAsync(message, cancellationToken);
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

        public void Dispose()
        {
            this.retryHandler.Dispose();
        }
    }
}
