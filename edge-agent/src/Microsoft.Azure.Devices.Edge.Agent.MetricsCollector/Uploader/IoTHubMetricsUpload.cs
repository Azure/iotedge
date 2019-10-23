namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class IoTHubMetricsUpload : IMetricsUpload
    {
        public async Task UploadAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }
    }
}
