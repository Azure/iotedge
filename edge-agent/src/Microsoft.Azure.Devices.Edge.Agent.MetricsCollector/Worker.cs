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
        readonly IScraper scraper;
        readonly IFileStorage storage;
        readonly IMetricsUpload uploader;
        readonly ISystemTime systemTime;
        readonly MetricsParser metricsParser = new MetricsParser();
        readonly AsyncLock scrapeUploadLock = new AsyncLock();

        DateTime lastUploadTime = DateTime.MinValue;
        Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

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

        async Task Scrape(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.GetLock(cancellationToken))
            {
                Console.WriteLine($"{DateTime.UtcNow}Scraping Metrics");
                List<Metric> metricsToPersist = new List<Metric>();

                foreach (var moduleResult in await this.scraper.ScrapeAsync(cancellationToken))
                {
                    var scrapedMetrics = this.metricsParser.ParseMetrics(this.systemTime.UtcNow, moduleResult.Value);

                    foreach (Metric scrapedMetric in scrapedMetrics)
                    {
                        // Get the previous scrape for this metric
                        if (this.metrics.TryGetValue(scrapedMetric.GetValuelessHash(), out Metric oldMetric))
                        {
                            // If the metric is unchanged, do nothing
                            if (oldMetric.Value.Equals(scrapedMetric.Value))
                            {
                                continue;
                            }

                            // if the metric has changed, write the previous metric to disk
                            metricsToPersist.Add(oldMetric);
                        }

                        // if new metric or metric changed, save to local buffer
                        this.metrics[scrapedMetric.GetValuelessHash()] = scrapedMetric;
                    }
                }

                if (metricsToPersist.Count != 0)
                {
                    Console.WriteLine($"{DateTime.UtcNow}Storing metrics");
                    this.storage.AddScrapeResult(Newtonsoft.Json.JsonConvert.SerializeObject(metricsToPersist));
                }
            }
        }

        async Task Upload(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.GetLock(cancellationToken))
            {
                Console.WriteLine($"{DateTime.UtcNow}Uploading Metrics");
                await this.uploader.UploadAsync(this.GetMetricsToUpload(this.lastUploadTime), cancellationToken);
                this.lastUploadTime = this.systemTime.UtcNow;
            }
        }

        IEnumerable<Metric> GetMetricsToUpload(DateTime lastUploadTime)
        {
            foreach (KeyValuePair<DateTime, Func<string>> data in this.storage.GetData(lastUploadTime))
            {
                var fileMetrics = Newtonsoft.Json.JsonConvert.DeserializeObject<Metric[]>(data.Value()) ?? Enumerable.Empty<Metric>();
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

    /// <summary>
    /// Used to ensure upload and scrape don't run simultaneously.
    /// </summary>
    class AsyncLock
    {
        public class OwnedLock : IDisposable
        {
            SemaphoreSlim semaphore;
            internal OwnedLock(SemaphoreSlim semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                this.semaphore.Release();
            }
        }

        SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public async Task<OwnedLock> GetLock(CancellationToken cancellationToken)
        {
            await this.semaphore.WaitAsync(cancellationToken);
            return new OwnedLock(this.semaphore);
        }
    }
}
