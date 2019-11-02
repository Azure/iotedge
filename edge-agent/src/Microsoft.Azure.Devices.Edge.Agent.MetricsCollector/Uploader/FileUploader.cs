// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class FileUploader : IMetricsUpload
    {
        public async Task UploadAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            using (FileStream file = File.OpenWrite("MetricsUpload"))
            {
                byte[] buffer = Encoding.UTF8.GetBytes($"\n\n\n{DateTime.UtcNow}New Upload");
                await file.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                foreach (Metric metric in metrics)
                {
                    buffer = Encoding.UTF8.GetBytes("\n" + Newtonsoft.Json.JsonConvert.SerializeObject(metric));
                    await file.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                }
            }
        }
    }
}
