// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Writes to a local file instead of uploading.
    /// </summary>
    public sealed class MetricsFileWriter : IMetricsPublisher
    {
        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory("shared");
            using (FileStream file = File.Open(@"shared/MetricsUpload.txt", FileMode.Append))
            {
                Console.WriteLine("Writing to file");
                byte[] buffer = Encoding.UTF8.GetBytes($"\n\n\nNew Upload: {DateTime.UtcNow}");
                await file.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                foreach (Metric metric in metrics)
                {
                    buffer = Encoding.UTF8.GetBytes("\n" + Newtonsoft.Json.JsonConvert.SerializeObject(metric));
                    await file.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                }
            }

            return true;
        }
    }
}
