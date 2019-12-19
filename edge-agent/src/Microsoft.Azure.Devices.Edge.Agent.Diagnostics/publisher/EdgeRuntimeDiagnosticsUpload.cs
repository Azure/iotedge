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
        static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeRuntimeDiagnosticsUpload>();
        readonly IEdgeAgentConnection edgeAgentConnection;

        public EdgeRuntimeDiagnosticsUpload(IEdgeAgentConnection edgeAgentConnection)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
        }

        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));
            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();

            if (data.Length > 0)
            {
                Message message = this.BuildMessage(data);

                try
                {
                    await this.edgeAgentConnection.SendEventAsync(message);
                }
                catch (Exception ex) when (ex.HasTimeoutException())
                {
                    Log.LogDebug(ex, "Send message to IoTHub");
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
    }
}
