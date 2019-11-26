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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class MetricsWorker : IDisposable
    {
        readonly IMetricsScraper scraper;
        readonly IMetricsStorage storage;
        readonly IMetricsPublisher uploader;
        readonly ISystemTime systemTime;
        readonly AsyncLock scrapeUploadLock = new AsyncLock();
        static readonly ILogger Log = Logger.Factory.CreateLogger<MetricsScraper>();

        DateTime lastUploadTime = DateTime.MinValue;

        // This acts as a local buffer. It stores the previous value of every metric.
        // If the new value for that metric is unchanged, it doesn't write the duplicate value to disk.
        Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

        PeriodicTask scrape;
        PeriodicTask upload;

        public MetricsWorker(IMetricsScraper scraper, IMetricsStorage storage, IMetricsPublisher uploader, ISystemTime systemTime = null)
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

        internal async Task Scrape(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation("Scraping Metrics");
                List<Metric> metricsToPersist = new List<Metric>();
                int numScrapedMetrics = 0;
                foreach (Metric scrapedMetric in await this.scraper.ScrapeEndpointsAsync(cancellationToken))
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
                    }

                    // if new metric or metric changed, save to local buffer and disk.
                    metricsToPersist.Add(scrapedMetric);
                    this.metrics[scrapedMetric.GetMetricKey()] = scrapedMetric;
                }

                Log.LogInformation($"Scraped {numScrapedMetrics} Metrics");

                if (metricsToPersist.Any())
                {
                    Log.LogInformation("Storing Metrics");
                    this.storage.WriteData(Newtonsoft.Json.JsonConvert.SerializeObject(metricsToPersist));
                    Log.LogInformation("Stored Metrics");
                }
            }
        }

        internal async Task Upload(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation($"Uploading Metrics. Last upload was at {this.lastUploadTime}");
                DateTime currentUploadTime = this.systemTime.UtcNow;
                int numMetricsUploaded = 0;
                IEnumerable<Metric> metricsToUpload = this.GetMetricsToUpload(this.lastUploadTime).Select(metric =>
                {
                    numMetricsUploaded++;
                    return metric;
                });
                await this.uploader.PublishAsync(metricsToUpload, cancellationToken);
                Log.LogInformation($"Uploaded {numMetricsUploaded} Metrics");

                this.storage.RemoveOldEntries(currentUploadTime);
                this.metrics.Clear();
                this.lastUploadTime = currentUploadTime;
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
        }

        public void Dispose()
        {
            this.scrape?.Dispose();
            this.upload?.Dispose();
        }
    }
}
