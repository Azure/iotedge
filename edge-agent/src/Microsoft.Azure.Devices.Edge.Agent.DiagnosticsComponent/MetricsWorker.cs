// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MetricsWorker : IDisposable
    {
        readonly IMetricsScraper scraper;
        readonly IMetricsFileStorage storage;
        readonly IMetricsUpload uploader;
        readonly ILogger logger;
        readonly ISystemTime systemTime;
        readonly AsyncLock scrapeUploadLock = new AsyncLock();

        DateTime lastUploadTime = DateTime.MinValue;
        Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

        PeriodicTask scrape;
        PeriodicTask upload;

        public MetricsWorker(IMetricsScraper scraper, IMetricsFileStorage storage, IMetricsUpload uploader, ILogger logger, ISystemTime systemTime = null)
        {
            this.scraper = scraper;
            this.storage = storage;
            this.uploader = uploader;
            this.logger = logger;
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public void Start(TimeSpan scrapingInterval, TimeSpan uploadInterval)
        {
            this.scrape = new PeriodicTask(this.Scrape, scrapingInterval, scrapingInterval, this.logger, "Scrape");
            this.upload = new PeriodicTask(this.Upload, uploadInterval, uploadInterval, this.logger, "Scrape");
        }

        async Task Scrape(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.GetLock(cancellationToken))
            {
                this.logger.LogInformation("Scraping Metrics");
                List<Metric> metricsToPersist = new List<Metric>();

                foreach (var moduleResult in await this.scraper.ScrapeAsync(cancellationToken))
                {
                    IEnumerable<Metric> scrapedMetrics = MetricsParser.ParseMetrics(this.systemTime.UtcNow, moduleResult.Value);

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

                this.logger.LogInformation("Scraped Metrics");

                if (metricsToPersist.Count != 0)
                {
                    this.logger.LogInformation("Storing Metrics");
                    this.storage.AddScrapeResult(Newtonsoft.Json.JsonConvert.SerializeObject(metricsToPersist));
                    this.logger.LogInformation("Stored Metrics");
                }
            }
        }

        async Task Upload(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.GetLock(cancellationToken))
            {
                this.logger.LogInformation("Uploading Metrics");
                await this.uploader.UploadAsync(this.GetMetricsToUpload(this.lastUploadTime), cancellationToken);
                this.lastUploadTime = this.systemTime.UtcNow;
                this.logger.LogInformation("Uploaded Metrics");
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

        public void Dispose()
        {
            this.scrape?.Dispose();
            this.upload?.Dispose();
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
