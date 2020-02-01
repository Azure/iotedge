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
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class MetricsWorker : IDisposable
    {
        public static RetryStrategy RetryStrategy = new ExponentialBackoff(20, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(1), false);

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
            if (!await this.TryUploadAndClear(cancellationToken))
            {
                await this.BeginUploadRetry(cancellationToken);
            }
        }

        async Task<bool> TryUploadAndClear(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation($"Uploading Metrics");
                IEnumerable<Metric> metricsToUpload = (await this.storage.GetAllMetricsAsync()).CondenseTimeSeries();

                try
                {
                    if (await this.uploader.PublishAsync(metricsToUpload, cancellationToken))
                    {
                        Log.LogInformation($"Published metrics");
                        await this.storage.RemoveAllReturnedMetricsAsync();
                        return true;
                    }
                    else
                    {
                        Log.LogInformation($"Failed to publish metrics");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    // If unexpected error publishing metrics, delete stored ones to prevent storage buildup.
                    Log.LogError($"Unexpected error publishing metrics. Deleting stored metrics.");
                    await this.storage.RemoveAllReturnedMetricsAsync();
                    throw ex;
                }
            }
        }

        async Task BeginUploadRetry(CancellationToken cancellationToken)
        {
            int retryNum = 0;
            var shouldRetry = RetryStrategy.GetShouldRetry();
            while (shouldRetry(retryNum++, null, out TimeSpan backoffDelay))
            {
                Log.LogInformation($"Metric publish set to retry in {backoffDelay.Humanize()}");
                await Task.Delay(backoffDelay, cancellationToken);
                if (await this.TryUploadAndClear(cancellationToken))
                {
                    // Upload succeded, end loop
                    return;
                }
            }

            Log.LogInformation($"Upload retries exeeded {retryNum} allowed attempts. Deleting stored metrics.");
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                await this.storage.RemoveAllReturnedMetricsAsync();
                Log.LogInformation($"Deleted stored metrics.");
            }
        }

        public void Dispose()
        {
            this.scrape?.Dispose();
            this.upload?.Dispose();
        }
    }
}
