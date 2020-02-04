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
        readonly List<string> filesToDelete = new List<string>();

        public MetricsFileStorage(string directory, ISystemTime systemTime = null)
        {
            this.directory = Preconditions.CheckNonWhiteSpace(directory, nameof(directory));
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public Task StoreMetricsAsync(IEnumerable<Metric> metrics)
        {
            return this.WriteData(JsonConvert.SerializeObject(metrics));
        }

        public Task<IEnumerable<Metric>> GetAllMetricsAsync()
        {
            return Directory.GetFiles(this.directory)
                .OrderBy(filename => filename)
                .SelectManyAsync<string, Metric>(async filename =>
                {
                    Metric[] fileMetrics;
                    try
                    {
                        string rawMetrics = await DiskFile.ReadAllAsync(filename);
                        fileMetrics = JsonConvert.DeserializeObject<Metric[]>(rawMetrics) ?? new Metric[0];
                        this.filesToDelete.Add(filename);
                    }
                    catch
                    {
                        fileMetrics = new Metric[0];
                    }

                    return fileMetrics;
                });
        }

        public async Task RemoveAllReturnedMetricsAsync()
        {
            await Task.Yield();
            foreach (string filename in this.filesToDelete)
            {
                File.Delete(filename);
            }

            this.filesToDelete.Clear();
        }

        Task WriteData(string data)
        {
            Directory.CreateDirectory(this.directory);
            string file = Path.Combine(this.directory, this.systemTime.UtcNow.Ticks.ToString());

            return DiskFile.WriteAllAsync(file, data);
        }
    }
}
