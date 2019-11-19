// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class IoTHubMetricsUpload : IMetricsUpload
    {
        readonly ModuleClient moduleClient;

        public IoTHubMetricsUpload(ModuleClient moduleClient)
        {
            this.moduleClient = moduleClient;
        }

        public async Task UploadAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            byte[] data = RawMetric.MetricsToBytes(metrics).ToArray();
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
