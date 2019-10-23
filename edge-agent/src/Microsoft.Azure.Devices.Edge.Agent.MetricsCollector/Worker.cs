// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Worker
    {
        private readonly IScraper scraper;
        private readonly IFileStorage storage;
        private readonly IMetricsUpload uploader;
        private readonly ISystemTime systemTime;
        private readonly MetricsParser metricsParser = new MetricsParser();
        private readonly AsyncLock scrapeUploadLock = new AsyncLock();

        private DateTime lastUploadTime = DateTime.MinValue;
        private Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

        public Worker(IScraper scraper, IFileStorage storage, IMetricsUpload uploader, ISystemTime systemTime = null)
        {
            this.scraper = scraper;
            this.storage = storage;
            this.uploader = uploader;
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public async Task Start(TimeSpan scrapingInterval, TimeSpan uploadInterval, CancellationToken cancellationToken)
        {
            Task scraper = Task.Run(
                async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(scrapingInterval, cancellationToken);
                    await this.Scrape(cancellationToken);
                }
            }, cancellationToken);

            Task uploader = Task.Run(
                async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(uploadInterval, cancellationToken);
                    await this.Upload(cancellationToken);
                }
            }, cancellationToken);

            await Task.WhenAll(scraper, uploader);
        }

        private async Task Scrape(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.GetLock(cancellationToken))
            {
                Console.WriteLine($"\n\n\nScraping Metrics");
                List<Metric> metricsToPersist = new List<Metric>();

                foreach (var moduleResult in await this.scraper.ScrapeAsync(cancellationToken))
                {
                    var scrapedMetrics = this.metricsParser.ParseMetrics(this.systemTime.UtcNow, moduleResult.Value);

                    foreach (Metric scrapedMetric in scrapedMetrics)
                    {
                        if (this.metrics.TryGetValue(scrapedMetric.GetValuelessHash(), out Metric oldMetric))
                        {
                            if (oldMetric.Value.Equals(scrapedMetric.Value))
                            {
                                continue;
                            }

                            metricsToPersist.Add(oldMetric);
                        }

                        this.metrics[scrapedMetric.GetValuelessHash()] = scrapedMetric;
                    }
                }

                if (metricsToPersist.Count != 0)
                {
                    Console.WriteLine($"Storing metrics");
                    this.storage.AddScrapeResult(Newtonsoft.Json.JsonConvert.SerializeObject(metricsToPersist));
                }
            }
        }

        private async Task Upload(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.GetLock(cancellationToken))
            {
                Console.WriteLine($"\n\n\nUploading Metrics");
                await this.uploader.UploadAsync(this.GetMetricsToUpload(this.lastUploadTime), cancellationToken);
                this.lastUploadTime = this.systemTime.UtcNow;
            }
        }

        private IEnumerable<Metric> GetMetricsToUpload(DateTime lastUploadTime)
        {
            foreach (KeyValuePair<DateTime, Func<string>> data in this.storage.GetData(lastUploadTime))
            {
                var temp = data.Value();
                var fileMetrics = Newtonsoft.Json.JsonConvert.DeserializeObject<Metric[]>(temp) ?? Enumerable.Empty<Metric>();
                foreach (Metric metric in fileMetrics)
                {
                    yield return metric;
                }
            }

            foreach (Metric metric in this.metrics.Values)
            {
                yield return metric;
            }

            this.storage.RemoveOldEntries(lastUploadTime);
            this.metrics.Clear();
        }
    }

    public class AsyncLock
    {
        public class OwnedLock : IDisposable
        {
            private SemaphoreSlim semaphore;
            internal OwnedLock(SemaphoreSlim semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                this.semaphore.Release();
            }
        }

        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public async Task<OwnedLock> GetLock(CancellationToken cancellationToken)
        {
            await this.semaphore.WaitAsync(cancellationToken);
            return new OwnedLock(this.semaphore);
        }
    }
}
