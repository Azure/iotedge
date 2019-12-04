// Copyright (c) Microsoft. All rights reserved.

[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test")]
namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class MetricsWorker : IDisposable
    {
        readonly IMetricsScraper scraper;
        readonly IMetricsStorage storage;
        readonly IMetricsPublisher uploader;
        readonly AsyncLock scrapeUploadLock = new AsyncLock();
        static readonly ILogger Log = Logger.Factory.CreateLogger<MetricsScraper>();

        DateTime lastUploadTime = DateTime.MinValue;

        // This acts as a local buffer. It stores the previous value of every metric.
        // If the new value for that metric is unchanged, it doesn't write the duplicate value to disk.
        Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

        PeriodicTask scrape;
        PeriodicTask upload;

        public MetricsWorker(IMetricsScraper scraper, IMetricsStorage storage, IMetricsPublisher uploader)
        {
            this.scraper = Preconditions.CheckNotNull(scraper, nameof(scraper));
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
            this.uploader = Preconditions.CheckNotNull(uploader, nameof(uploader));
        }

        public void Start(TimeSpan scrapingInterval, TimeSpan uploadInterval)
        {
            this.scrape = new PeriodicTask(this.Scrape, scrapingInterval, scrapingInterval, Log, "Metrics Scrape");
            this.upload = new PeriodicTask(this.Upload, uploadInterval, uploadInterval, Log, "Metrics Upload");
        }

        internal async Task Scrape(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation("Scraping Metrics");
                IEnumerable<Metric> scrapedMetrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
                Log.LogInformation($"Scraped Metrics");

                IEnumerable<Metric> metricsToStore = this.RemoveDuplicateMetrics(scrapedMetrics);
                Log.LogInformation("Storing Metrics");
                await this.storage.StoreMetricsAsync(metricsToStore);
                Log.LogInformation("Stored Metrics");
            }
        }

        internal async Task Upload(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation($"Uploading Metrics");
                IEnumerable<Metric> metricsToUpload = await this.storage.GetAllMetricsAsync();
                await this.uploader.PublishAsync(metricsToUpload, cancellationToken);
                Log.LogInformation($"Uploaded Metrics");

                // Clear local cache so all metrics are stored next scrape
                this.metrics.Clear();
            }
        }

        public void Dispose()
        {
            this.scrape?.Dispose();
            this.upload?.Dispose();
        }

        private IEnumerable<Metric> RemoveDuplicateMetrics(IEnumerable<Metric> newMetrics)
        {
            foreach (Metric newMetric in newMetrics)
            {
                // Get the previous scrape for this metric
                if (this.metrics.TryGetValue(newMetric.GetMetricKey(), out Metric oldMetric))
                {
                    // If the metric is unchanged, do nothing
                    if (oldMetric.Value.Equals(newMetric.Value))
                    {
                        continue;
                    }
                }

                // if new metric or metric changed, save to local buffer and disk.
                this.metrics[newMetric.GetMetricKey()] = newMetric;
                yield return newMetric;
            }
        }
    }
}
