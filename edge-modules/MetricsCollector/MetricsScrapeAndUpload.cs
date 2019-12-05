// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
<<<<<<< HEAD
    using Newtonsoft.Json;
    using System.Linq;
=======
>>>>>>> a497743cc950764196c07c5b7245478e777ba1f6

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
<<<<<<< HEAD
                IEnumerable<Metric> metricsWithTags = metrics.Select(metric => {
                    metric.Tags.Add("guid", this.guid.ToString());
=======
                IEnumerable<Metric> metricsWithTags = metrics.Select(metric =>
                {
                    metric.Tags.Add("guid", this.testId.ToString());
>>>>>>> a497743cc950764196c07c5b7245478e777ba1f6
                    return metric;
                });

                await this.publisher.PublishAsync(metricsWithTags, cancellationToken);

                Logger.LogInformation("Successfully scraped and uploaded metrics");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error scraping and uploading metrics");
            }
        }
    }
}
