// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public sealed class LogAnalyticsUpload : IMetricsPublisher
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<LogAnalyticsUpload>();
        readonly string workspaceId;
        readonly string workspaceKey;
        readonly string logType;

        public LogAnalyticsUpload(string workspaceId, string workspaceKey, string logType)
        {
            this.workspaceId = Preconditions.CheckNonWhiteSpace(workspaceId, nameof(workspaceId));
            this.workspaceKey = Preconditions.CheckNonWhiteSpace(workspaceKey, nameof(workspaceKey));
            this.logType = Preconditions.CheckNonWhiteSpace(logType, nameof(logType));
        }

        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            try
            {
                Preconditions.CheckNotNull(metrics, nameof(metrics));
                IEnumerable<UploadMetric> metricsToUpload = metrics.Select(m => new UploadMetric(m));
                await AzureLogAnalytics.Instance.PostAsync(this.workspaceId, this.workspaceKey, JsonConvert.SerializeObject(metricsToUpload), this.logType);
                Log.LogInformation($"Successfully sent metrics to LogAnalytics");
                return true;
            }
            catch (Exception e)
            {
                Log.LogError(e, "Error uploading metrics to LogAnalytics");
                return false;
            }
        }

        class UploadMetric
        {
            public DateTime TimeGeneratedUtc { get; }
            public string Name { get; }
            public double Value { get; }
            public string Tags { get; }

            public UploadMetric(Metric metric)
            {
                this.TimeGeneratedUtc = metric.TimeGeneratedUtc;
                this.Name = metric.Name;
                this.Value = metric.Value;
                this.Tags = JsonConvert.SerializeObject(metric.Tags);
            }
        }
    }
}
