// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
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
            Message message = new Message(data.ToArray());

            // TODO: add check for too big of a message
            await this.moduleClient.SendEventAsync(message);
        }
    }
}
