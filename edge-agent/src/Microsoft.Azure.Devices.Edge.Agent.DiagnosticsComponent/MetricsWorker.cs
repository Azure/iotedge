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
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class MetricsWorker : IDisposable
    {
        readonly IMetricsScraper scraper;
        readonly IMetricsStorage storage;
        readonly IMetricsUpload uploader;
        readonly ISystemTime systemTime;
        readonly AsyncLock scrapeUploadLock = new AsyncLock();
        static readonly ILogger Log = Logger.Factory.CreateLogger<MetricsScraper>();

        DateTime lastUploadTime = DateTime.MinValue;

        // This acts as a local buffer. It stores the previous value of every metric.
        // If the new value for that metric is unchanged, it doesn't write the duplicate value to disk.
        Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

        PeriodicTask scrape;
        PeriodicTask upload;

        public MetricsWorker(IMetricsScraper scraper, IMetricsStorage storage, IMetricsUpload uploader, ISystemTime systemTime = null)
        {
            this.scraper = Preconditions.CheckNotNull(scraper, nameof(scraper));
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
            this.uploader = Preconditions.CheckNotNull(uploader, nameof(uploader));
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public void Start(TimeSpan scrapingInterval, TimeSpan uploadInterval)
        {
            this.scrape = new PeriodicTask(this.Scrape, scrapingInterval, scrapingInterval, Log, "Metrics Scrape");
            this.upload = new PeriodicTask(this.Upload, uploadInterval, uploadInterval, Log, "Metrics Upload");
        }

        async Task Scrape(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation("Scraping Metrics");
                List<Metric> metricsToPersist = new List<Metric>();
                int numScrapedMetrics = 0;
                foreach (var scrapedMetric in await this.scraper.ScrapeAsync(cancellationToken))
                {
                    numScrapedMetrics++;
                    // Get the previous scrape for this metric
                    if (this.metrics.TryGetValue(scrapedMetric.GetMetricKey(), out Metric oldMetric))
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
                    this.metrics[scrapedMetric.GetMetricKey()] = scrapedMetric;
                }

                Log.LogInformation($"Scraped {numScrapedMetrics} Metrics");

                if (metricsToPersist.Count != 0)
                {
                    Log.LogInformation("Storing Metrics");
                    this.storage.WriteData(Newtonsoft.Json.JsonConvert.SerializeObject(metricsToPersist));
                    Log.LogInformation("Stored Metrics");
                }
            }
        }

        async Task Upload(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation("Uploading Metrics");
                await this.uploader.UploadAsync(this.GetMetricsToUpload(this.lastUploadTime), cancellationToken);
                Log.LogInformation("Uploaded Metrics");

                this.storage.RemoveOldEntries(this.lastUploadTime);
                this.metrics.Clear();
                this.lastUploadTime = this.systemTime.UtcNow;
            }
        }

        IEnumerable<Metric> GetMetricsToUpload(DateTime lastUploadTime)
        {
            // Get all metrics that have been stored since the last upload
            foreach (KeyValuePair<DateTime, Func<string>> data in this.storage.GetData(lastUploadTime))
            {
                var fileMetrics = Newtonsoft.Json.JsonConvert.DeserializeObject<Metric[]>(data.Value()) ?? Enumerable.Empty<Metric>();
                foreach (Metric metric in fileMetrics)
                {
                    yield return metric;
                }
            }

            // Get all metrics stored in the local buffer.
            foreach (Metric metric in this.metrics.Values)
            {
                yield return metric;
            }
        }

        public void Dispose()
        {
            this.scrape?.Dispose();
            this.upload?.Dispose();
        }
    }
}
