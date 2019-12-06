// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class MetricsScrapeAndUpload : IDisposable
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        readonly IMetricsScraper scraper;
        readonly IMetricsPublisher publisher;
        PeriodicTask periodicScrapeAndUpload;
        Guid testId; // used for differentiating between test runs on the same device

        public MetricsScrapeAndUpload(IMetricsScraper scraper, IMetricsPublisher publisher, Guid testId)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
            Preconditions.CheckArgument(testId != Guid.Empty);
            this.testId = testId;
        }

        public void Start(TimeSpan scrapeAndUploadInterval)
        {
            this.periodicScrapeAndUpload = new PeriodicTask(this.ScrapeAndUploadMetricsAsync, scrapeAndUploadInterval, scrapeAndUploadInterval, Logger, "Scrape and Upload Metrics");
        }

        public void Dispose()
        {
           this.periodicScrapeAndUpload?.Dispose();
        }

        async Task ScrapeAndUploadMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IEnumerable<Metric> metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);

                await this.publisher.PublishAsync(this.GetGuidTaggedMetrics(metrics), cancellationToken);

                Logger.LogInformation("Successfully scraped and uploaded metrics");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error scraping and uploading metrics");
            }
        }

        IEnumerable<Metric> GetGuidTaggedMetrics(IEnumerable<Metric> metrics)
        {
            foreach (Metric metric in metrics)
            {
                IReadOnlyDictionary<string, string> customTags = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "guid", this.testId.ToString() } } );
                yield return new MetricWithCustomTags(metric, customTags);
            }
        }
    }
}
