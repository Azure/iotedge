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

        public FixedSetTableUpload(string dataCollectionEndpoint, string dataCollectionRuleId, string streamName, TokenCredential credential)
        {
            Preconditions.CheckNonWhiteSpace(dataCollectionEndpoint, nameof(dataCollectionEndpoint));
            this.dataCollectionRuleId = Preconditions.CheckNonWhiteSpace(dataCollectionRuleId, nameof(dataCollectionRuleId));
            this.streamName = Preconditions.CheckNonWhiteSpace(streamName, nameof(streamName));
            this.client = new LogsIngestionClient(new Uri(dataCollectionEndpoint), Preconditions.CheckNotNull(credential, nameof(credential)));

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
                List<LaMetric> metricsToUpload = metrics.Select(m => new LaMetric(m, DNSName)).ToList();
                bool success = false;
                for (int i = 0; i < Constants.UploadMaxRetries && (!success); i++)
                {
                    try
                    {
                        // The SDK handles batching/compression of the payload internally.
                        Response response = await this.client.UploadAsync(this.dataCollectionRuleId, this.streamName, metricsToUpload, cancellationToken: cancellationToken).ConfigureAwait(false);
                        success = !response.IsError;
                        if (!success)
                        {
                            LoggerUtil.Writer.LogDebug($"Logs ingestion upload failed - status {response.Status}, reason {response.ReasonPhrase}");
                        }
                    }
                    catch (Exception e)
                    {
                        // Retry on a per-attempt basis: covers both transient network/service
                        // failures and known Azure.Monitor.Ingestion SDK bugs (e.g. a null
                        // reference thrown from LogsIngestionClient.UploadAsync when no upload
                        // task reaches its internal concurrency threshold before being aborted).
                        LoggerUtil.Writer.LogDebug(e, "Logs ingestion upload attempt threw an exception");
                    }
                }

                if (success)
                    LoggerUtil.Writer.LogInformation($"Successfully sent {metricsToUpload.Count} metrics to fixed set table");
                else
                    LoggerUtil.Writer.LogError($"Failed to send {metricsToUpload.Count} metrics to fixed set table after {Constants.UploadMaxRetries} retries");
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
            public LaMetric(Metric metric, string hostname)
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

                //TODO: what to do with origin?
            }
        }
    }
}

