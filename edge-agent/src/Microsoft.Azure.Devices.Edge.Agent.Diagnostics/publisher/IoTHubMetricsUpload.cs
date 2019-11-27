// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class IoTHubMetricsUpload : IMetricsPublisher
    {
        readonly ModuleClient moduleClient;

        public IoTHubMetricsUpload(ModuleClient moduleClient)
        {
            this.moduleClient = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Preconditions.CheckNotNull(metrics, nameof(metrics));
            byte[] data = MetricsSerializer.MetricsToBytes(metrics).ToArray();
            byte[] compressedData = Compression.CompressToGzip(data);

            // TODO: add check for too big of a message
            if (compressedData.Length > 0)
            {
                Message message = new Message(compressedData);
                await this.moduleClient.SendEventAsync(message);
            }
        }
    }
}
