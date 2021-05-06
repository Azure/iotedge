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
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggregation;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class MetricsWorker : IDisposable
    {
        public static RetryStrategy RetryStrategy = new ExponentialBackoff(20, TimeSpan.FromMinutes(5), TimeSpan.FromHours(12), TimeSpan.FromMinutes(1), false);

        readonly IMetricsScraper scraper;
        readonly IMetricsStorage storage;
        readonly IMetricsPublisher uploader;
        readonly AsyncLock scrapeUploadLock = new AsyncLock();
        static readonly ILogger Log = Logger.Factory.CreateLogger<MetricsScraper>();
        readonly MetricTransformer metricFilter;
        readonly MetricAggregator metricAggregator;

        PeriodicTask scrape;
        PeriodicTask upload;

        public MetricsWorker(IMetricsScraper scraper, IMetricsStorage storage, IMetricsPublisher uploader)
        {
            this.scraper = Preconditions.CheckNotNull(scraper, nameof(scraper));
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
            this.uploader = Preconditions.CheckNotNull(uploader, nameof(uploader));

            this.metricFilter = new MetricTransformer()
                .AddAllowedTags((MetricsConstants.MsTelemetry, true.ToString()))
                .AddDisallowedTags(
                    ("quantile", "0.1"),
                    ("quantile", "0.5"),
                    ("quantile", "0.99"))
                .AddTagsToRemove(MetricsConstants.MsTelemetry, MetricsConstants.IotHubLabel, MetricsConstants.DeviceIdLabel)
                .AddTagsToModify(
                    ("id", this.ReplaceDeviceId),
                    ("module_name", this.ReplaceModuleId),
                    ("to", name => name.CreateSha256()),
                    ("from", name => name.CreateSha256()),
                    ("to_route_input", name => name.CreateSha256()),
                    ("from_route_output", name => name.CreateSha256()));

#pragma warning disable SA1111 // Closing parenthesis should be on line of last parameter
            this.metricAggregator = new MetricAggregator(
                new AggregationTemplate("edgehub_gettwin_total", "id", new Summer()),
                new AggregationTemplate(
                    "edgehub_messages_received_total",
                    ("route_output", new Summer()),
                    ("id", new Summer())
                ),
                new AggregationTemplate(
                    "edgehub_messages_sent_total",
                    ("from", new Summer()),
                    ("to", new Summer()),
                    ("from_route_output", new Summer()),
                    ("to_route_input", new Summer())
                ),
                new AggregationTemplate(
                    new string[]
                    {
                        "edgehub_message_size_bytes",
                        "edgehub_message_size_bytes_sum",
                        "edgehub_message_size_bytes_count"
                    },
                    "id",
                    new Averager()),
                new AggregationTemplate(
                    new string[]
                    {
                        "edgehub_message_process_duration_seconds",
                        "edgehub_message_process_duration_seconds_sum",
                        "edgehub_message_process_duration_seconds_count",
                    },
                    ("from", new Averager()),
                    ("to", new Averager())
                ),
                new AggregationTemplate(
                    "edgehub_direct_methods_total",
                    ("from", new Summer()),
                    ("to", new Summer())
                ),
                new AggregationTemplate("edgehub_queue_length", "endpoint", new Summer()),
                new AggregationTemplate(
                    new string[]
                    {
                        "edgehub_messages_dropped_total",
                        "edgehub_messages_unack_total",
                    },
                    ("from", new Summer()),
                    ("from_route_output", new Summer())
                ),
                new AggregationTemplate("edgehub_client_connect_failed_total", "id", new Summer())
           );
#pragma warning restore SA1111 // Closing parenthesis should be on line of last parameter
        }

        public void Start(TimeSpan scrapingInterval, TimeSpan uploadInterval)
        {
            this.scrape = new PeriodicTask(this.Scrape, scrapingInterval, scrapingInterval, Log, "Metrics Scrape");
            TimeSpan uploadJitter = new TimeSpan((long)(uploadInterval.Ticks * new Random().NextDouble()));
            this.upload = new PeriodicTask(this.Upload, uploadJitter, uploadInterval, Log, "Metrics Upload");
        }

        internal async Task Scrape(CancellationToken cancellationToken)
        {
            using (await this.scrapeUploadLock.LockAsync(cancellationToken))
            {
                Log.LogInformation("Scraping Metrics");
                IEnumerable<Metric> scrapedMetrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);

                scrapedMetrics = this.metricFilter.TransformMetrics(scrapedMetrics);
                scrapedMetrics = this.metricAggregator.AggregateMetrics(scrapedMetrics);

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

        /// <summary>
        /// Replaces the device id in some edgeHub metrics with "device".
        ///
        /// EdgeHub metrics id comes in the form of 'deviceId/moduleName' in the case of an edgeDevice
        /// and 'deviceId' in the case of downstream leaf devices.
        /// </summary>
        /// <param name="id">Metric id tag.</param>
        /// <returns>Id tag with deviceId removed.</returns>
        string ReplaceDeviceId(string id)
        {
            const string deviceIdReplacement = "device";

            // Id is in the form of 'deviceId/moduleId'
            string[] parts = id.Split('/');
            if (parts.Length == 2)
            {
                return $"{deviceIdReplacement}/{this.ReplaceModuleId(parts[1])}";
            }

            // Id is just 'deviceId'
            return deviceIdReplacement;
        }

        string ReplaceModuleId(string id)
        {
            // Don't hash system modules
            if (id.StartsWith("$"))
            {
                return id;
            }

            return id.CreateSha256();
        }

        public void Dispose()
        {
            this.scrape?.Dispose();
            this.upload?.Dispose();
        }
    }
}
