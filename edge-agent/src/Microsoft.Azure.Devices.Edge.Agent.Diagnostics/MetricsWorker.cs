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
                Log.LogInformation("Storing Metrics");
                await this.storage.StoreMetricsAsync(scrapedMetrics);
                Log.LogInformation("Scraped and Stored Metrics");
            }
        }

        internal async Task Upload(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation($"Uploading Metrics");
                IEnumerable<Metric> metricsToUpload = await this.storage.GetAllMetricsAsync();
                metricsToUpload = RemoveDuplicateMetrics(metricsToUpload);
                await this.uploader.PublishAsync(metricsToUpload, cancellationToken);
                Log.LogInformation($"Uploaded Metrics");
                await this.storage.RemoveAllReturnedMetricsAsync();
            }
        }

        internal static IEnumerable<Metric> RemoveDuplicateMetrics(IEnumerable<Metric> metrics)
        {
            Dictionary<int, Metric> previousValues = new Dictionary<int, Metric>();

            foreach (Metric newMetric in metrics)
            {
                int key = newMetric.GetMetricKey();
                // Get the previous value for this metric. If unchanged, return old
                if (previousValues.TryGetValue(key, out Metric oldMetric) && oldMetric.Value != newMetric.Value)
                {
                    yield return oldMetric;
                }

                // update previous
                previousValues[key] = newMetric;
            }

            foreach (Metric remainingMetric in previousValues.Values)
            {
                yield return remainingMetric;
            }
        }

        public void Dispose()
        {
            this.scrape?.Dispose();
            this.upload?.Dispose();
        }
    }
}
