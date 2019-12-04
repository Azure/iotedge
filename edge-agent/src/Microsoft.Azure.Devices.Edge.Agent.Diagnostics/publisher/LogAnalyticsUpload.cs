// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
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
        readonly Guid guid;

        public LogAnalyticsUpload(string workspaceId, string workspaceKey, string logType, Guid guid)
        {
            this.workspaceId = Preconditions.CheckNonWhiteSpace(workspaceId, nameof(workspaceId));
            this.workspaceKey = Preconditions.CheckNonWhiteSpace(workspaceKey, nameof(workspaceKey));
            this.logType = Preconditions.CheckNonWhiteSpace(logType, nameof(logType));
            this.guid = Preconditions.CheckNotNull(guid);
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            try
            {
                Preconditions.CheckNotNull(metrics, nameof(metrics));
                await AzureLogAnalytics.Instance.PostAsync(this.workspaceId, this.workspaceKey, JsonConvert.SerializeObject(metrics), this.logType);
                Log.LogInformation($"Successfully sent metrics to LogAnalytics");
            }
            catch (Exception e)
            {
                Log.LogError($"Error uploading metrics to LogAnalytics: {e}");
            }
        }
    }
}
