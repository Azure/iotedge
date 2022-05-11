// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Util;

    class MetricsScrapeAndUpload : IDisposable
    {
        private readonly MetricsScraper scraper;
        private readonly IMetricsPublisher publisher;
        private PeriodicTask periodicScrapeAndUpload;

        public MetricsScrapeAndUpload(MetricsScraper scraper, IMetricsPublisher publisher)
        {
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.publisher = Preconditions.CheckNotNull(publisher);
        }

        public void Start(TimeSpan scrapeAndUploadInterval)
        {
            this.periodicScrapeAndUpload = new PeriodicTask(this.ScrapeAndUploadMetricsAsync, scrapeAndUploadInterval, TimeSpan.FromMinutes(1), LoggerUtil.Writer, "Scrape and Upload Metrics", true);
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

                // filter metrics
                // if the allowed list is non-empty then only accept metrics on the allow list
                if (!Settings.Current.AllowedMetrics.Empty)
                    metrics = metrics.Where(x => Settings.Current.AllowedMetrics.Matches(x));
                // always use the disallow list
                metrics = metrics.Where(x => !Settings.Current.BlockedMetrics.Matches(x));

                await this.publisher.PublishAsync(metrics, cancellationToken);
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError(e, "Error scraping and uploading metrics");
            }
        }
    }
}
