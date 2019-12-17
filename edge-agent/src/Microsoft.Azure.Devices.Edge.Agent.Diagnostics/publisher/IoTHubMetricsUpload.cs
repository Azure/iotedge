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
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class IoTHubMetricsUpload : IMetricsPublisher
    {
        readonly IEdgeAgentConnection edgeAgentConnection;

        public IoTHubMetricsUpload(IEdgeAgentConnection edgeAgentConnection)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));
            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();

            // TODO: add check for too big of a message
            if (data.Length > 0)
            {
                Message message = this.BuildMessage(data);
                await this.edgeAgentConnection.SendEventAsync(message);
            }
        }

        Message BuildMessage(byte[] data)
        {
            Message message = new Message(data);
            message.ContentType = "application/x-azureiot-edgeruntimediagnostics";

            return message;
        }
    }
}
