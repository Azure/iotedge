// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class StdoutUploader : IMetricsUpload
    {
        public Task UploadAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Console.WriteLine($"\n\n\n{DateTime.UtcNow}Metric Upload");
            foreach (Metric metric in metrics)
            {
                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(metric));
            }

            return Task.CompletedTask;
        }
    }
}
