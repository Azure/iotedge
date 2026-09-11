// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using global::Azure.Monitor.Ingestion;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class FixedSetTableUpload : IMetricsPublisher
    {
        private readonly LogsIngestionClient client;
        private readonly string dataCollectionRuleId;
        private readonly string streamName;
        private readonly string DNSName;
        private readonly string resourceId;

        public FixedSetTableUpload(string dataCollectionEndpoint, string dataCollectionRuleId, string streamName, string resourceId, TokenCredential credential, LogsIngestionAudience audience)
        {
            Preconditions.CheckNonWhiteSpace(dataCollectionEndpoint, nameof(dataCollectionEndpoint));
            this.dataCollectionRuleId = Preconditions.CheckNonWhiteSpace(dataCollectionRuleId, nameof(dataCollectionRuleId));
            this.streamName = Preconditions.CheckNonWhiteSpace(streamName, nameof(streamName));
            this.resourceId = Preconditions.CheckNonWhiteSpace(resourceId, nameof(resourceId));
            var options = new LogsIngestionClientOptions { Audience = audience };
            this.client = new LogsIngestionClient(new Uri(dataCollectionEndpoint), Preconditions.CheckNotNull(credential, nameof(credential)), options);

            string DNSName = Environment.GetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME");
            if (DNSName == null || String.IsNullOrEmpty(DNSName))
            {
                // TODO: is this a good fallback?
                // TODO: test
                DNSName = Dns.GetHostName();
            }
            this.DNSName = DNSName;
        }

        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            try
            {
                Preconditions.CheckNotNull(metrics, nameof(metrics));

                // NaN/Infinity values (e.g. summary quantiles with no recent samples, see
                // BuiltInMetrics.md) can't be represented in standard JSON. System.Text.Json,
                // which the SDK uses internally to serialize each metric, throws on them, and
                // the SDK swallows that exception rather than surfacing it, so an unfiltered
                // batch fails silently on every upload. Drop them instead of uploading.
                int skipped = 0;
                List<LaMetric> metricsToUpload = metrics
                    .Where(m =>
                    {
                        bool finite = !double.IsNaN(m.Value) && !double.IsInfinity(m.Value);
                        if (!finite)
                        {
                            skipped++;
                        }
                        return finite;
                    })
                    .Select(m => new LaMetric(m, DNSName, this.resourceId))
                    .ToList();
                if (skipped > 0)
                {
                    LoggerUtil.Writer.LogDebug($"Skipped {skipped} metrics with a NaN or infinite value; these can't be represented in JSON.");
                }

                bool success = false;
                Exception lastException = null;
                string lastFailure = null;
                if (metricsToUpload.Count == 0)
                {
                    LoggerUtil.Writer.LogDebug("No metrics with finite values to upload this cycle.");
                    return true;
                }

                for (int i = 0; i < Constants.UploadMaxRetries && (!success); i++)
                {
                    try
                    {
                        // The SDK handles batching/compression of the payload internally.
                        Response response = await this.client.UploadAsync(this.dataCollectionRuleId, this.streamName, metricsToUpload, cancellationToken: cancellationToken).ConfigureAwait(false);
                        success = !response.IsError;
                        if (!success)
                        {
                            lastFailure = $"status {response.Status}, reason {response.ReasonPhrase}";
                            LoggerUtil.Writer.LogDebug($"Logs ingestion upload failed - status {response.Status}, reason {response.ReasonPhrase}");
                        }
                    }
                    catch (Exception e)
                    {
                        lastException = e;
                        // Retry on a per-attempt basis: covers transient network/service failures,
                        // and a real Azure.Monitor.Ingestion SDK bug where a non-cancellation
                        // exception thrown while serializing a log entry (e.g. a NaN/Infinity
                        // value slipping through) is silently swallowed internally, leaving no
                        // upload task queued and causing a NullReferenceException on return.
                        LoggerUtil.Writer.LogDebug(e, "Logs ingestion upload attempt threw an exception");
                    }
                }

                if (success)
                    LoggerUtil.Writer.LogInformation($"Successfully sent {metricsToUpload.Count} metrics to fixed set table");
                else if (lastException != null)
                    LoggerUtil.Writer.LogError(lastException, $"Failed to send {metricsToUpload.Count} metrics to fixed set table after {Constants.UploadMaxRetries} retries. Last exception: {lastException.Message}");
                else
                    LoggerUtil.Writer.LogError($"Failed to send {metricsToUpload.Count} metrics to fixed set table after {Constants.UploadMaxRetries} retries. Last response: {lastFailure}");
                return success;
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError(e, "Error uploading metrics to fixed set table");
                return false;
            }
        }

        private class LaMetric
        {
            public string Origin { get; }
            public string Namespace { get; }
            public string Name { get; }
            public double Value { get; }
            public DateTime CollectionTime { get; }
            public string Tags { get; }
            public string Computer { get; }
            public string ResourceId { get; }
            public LaMetric(Metric metric, string hostname, string resourceId)
            {
                // forms DB key
                this.Name = metric.Name;
                this.Tags = JsonConvert.SerializeObject(metric.Tags);

                // value
                this.Value = metric.Value;

                // optional 
                this.CollectionTime = metric.TimeGeneratedUtc;
                this.Computer = Constants.MetricComputer;
                this.Origin = Constants.MetricOrigin;
                this.Namespace = Constants.MetricNamespace;
                this.ResourceId = resourceId;

                //TODO: what to do with origin?
            }
        }
    }
}

