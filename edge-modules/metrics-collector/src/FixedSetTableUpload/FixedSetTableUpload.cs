// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class FixedSetTableUpload : IMetricsPublisher
    {
        private readonly string workspaceId;
        private readonly string workspaceKey;
        private readonly string DNSName;

        public FixedSetTableUpload(string workspaceId, string workspaceKey)
        {
            this.workspaceId = Preconditions.CheckNonWhiteSpace(workspaceId, nameof(workspaceId));
            this.workspaceKey = Preconditions.CheckNonWhiteSpace(workspaceKey, nameof(workspaceKey));

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
                IEnumerable<LaMetric> metricsToUpload = metrics.Select(m => new LaMetric(m, DNSName));
                LaMetricList metricList = new LaMetricList(metricsToUpload);
                bool success = false;
                for (int i = 0; i < Constants.UploadMaxRetries && (!success); i++)
                {
                    // TODO: split up metricList so that no individual post is greater than 1mb
                    success = await AzureFixedSetTable.Instance.PostAsync(this.workspaceId, this.workspaceKey, JsonConvert.SerializeObject(metricList), Settings.Current.ResourceId);
                }

                if (success)
                    LoggerUtil.Writer.LogInformation($"Successfully sent {metricList.DataItems.Count()} metrics to fixed set table");
                else
                    LoggerUtil.Writer.LogError($"Failed to send {metricList.DataItems.Count()} metrics to fixed set table after {Constants.UploadMaxRetries} retries");
                return success;
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError(e, "Error uploading metrics to fixed set table");
                return false;
            }
        }

        private class LaMetricList
        {
            public string DataType => Constants.MetricUploadDataType;
            public string IPName => Constants.MetricUploadIPName;
            public IEnumerable<LaMetric> DataItems { get; }

            public LaMetricList(IEnumerable<LaMetric> items)
            {
                DataItems = items;
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
