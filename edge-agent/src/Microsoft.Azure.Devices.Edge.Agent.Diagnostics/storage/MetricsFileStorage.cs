// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public sealed class MetricsFileStorage : IMetricsStorage
    {
        readonly string directory;
        readonly ISystemTime systemTime;

        public MetricsFileStorage(string directory, ISystemTime systemTime = null)
        {
            this.directory = Preconditions.CheckNonWhiteSpace(directory, nameof(directory));
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public async Task StoreMetricsAsync(IEnumerable<Metric> metrics)
        {
            await Task.Yield();

            this.WriteData(JsonConvert.SerializeObject(metrics));
        }

        public async Task<IEnumerable<Metric>> GetAllMetricsAsync()
        {
            await Task.Yield();

            return Directory.GetFiles(this.directory)
                .SelectMany(filename =>
                {
                    Metric[] fileMetrics;
                    try
                    {
                        string rawMetrics = File.ReadAllText(filename);
                        fileMetrics = JsonConvert.DeserializeObject<Metric[]>(rawMetrics) ?? new Metric[0];
                        File.Delete(filename);
                    }
                    catch
                    {
                        fileMetrics = new Metric[0];
                    }

                    return fileMetrics;
                });
        }

        private void WriteData(string data)
        {
            Directory.CreateDirectory(this.directory);
            string file = Path.Combine(this.directory, this.systemTime.UtcNow.Ticks.ToString());
            File.WriteAllText(file, data);
        }
    }
}
