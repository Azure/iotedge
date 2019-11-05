// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class IoTHubMetricsUpload : IMetricsUpload
    {
        readonly ModuleClient moduleClient;

        public IoTHubMetricsUpload(ModuleClient moduleClient)
        {
            this.moduleClient = moduleClient;
        }

        public async Task UploadAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            IEnumerable<byte> data = RawMetric.MetricsToBytes(metrics);
            byte[] compressedData = DeflateSerializer.Compress(data);

            // TODO: add check for too big of a message
            Message message = new Message(compressedData);
            await this.moduleClient.SendEventAsync(message);
        }
    }
}
