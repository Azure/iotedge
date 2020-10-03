// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public sealed class EdgeRuntimeDiagnosticsUpload : IMetricsPublisher
    {
        // Max message size is 256KB, keep a buffer for safety.
        // Absolute maximum is 256*1024 bytes total, but to be extra safe I kept it under 256000.
        // It doesn't increase customer bandwidth, which is the biggest concern.
        const int MaxMessageSize = 255000;

        static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeRuntimeDiagnosticsUpload>();
        readonly IEdgeAgentConnection edgeAgentConnection;

        public EdgeRuntimeDiagnosticsUpload(IEdgeAgentConnection edgeAgentConnection)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
        }

        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));

            Message[] messagesToSend = this.BatchAndBuildMessages(metrics.ToArray()).ToArray();
            try
            {
                await this.edgeAgentConnection.SendEventBatchAsync(messagesToSend);
            }
            catch (Exception ex) when (ex.HasTimeoutException())
            {
                Log.LogDebug(ex, "Send message to IoTHub");
                return false;
            }

            return true;
        }

        IEnumerable<Message> BatchAndBuildMessages(ArraySegment<Metric> metrics)
        {
            if (!metrics.Any())
            {
                return Enumerable.Empty<Message>();
            }

            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();
            if (data.Length > MaxMessageSize)
            {
                var part1 = this.BatchAndBuildMessages(metrics.Slice(0, metrics.Count / 2));
                var part2 = this.BatchAndBuildMessages(metrics.Slice(metrics.Count / 2));

                return part1.Concat(part2);
            }
            else
            {
                return new Message[] { this.BuildMessage(data) };
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
