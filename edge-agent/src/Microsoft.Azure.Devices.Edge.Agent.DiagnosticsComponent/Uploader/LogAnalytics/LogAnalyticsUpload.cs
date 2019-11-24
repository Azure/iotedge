// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
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

        public LogAnalyticsUpload(string workspaceId, string workspaceKey, string logType)
        {
            this.workspaceId = Preconditions.CheckNonWhiteSpace(workspaceId, nameof(workspaceId));
            this.workspaceKey = Preconditions.CheckNonWhiteSpace(workspaceKey, nameof(workspaceKey));
            this.logType = Preconditions.CheckNonWhiteSpace(logType, nameof(logType));
        }

        public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            try
            {
                await AzureLogAnalytics.Instance.PostAsync(this.workspaceId, this.workspaceKey, JsonConvert.SerializeObject(metrics), this.logType);
                Log.LogInformation($"Sent metrics to LogAnalytics");
            }
            catch (Exception e)
            {
                Log.LogError($"Error uploading metrics to LogAnalytics: {e}");
            }
        }
    }
}
