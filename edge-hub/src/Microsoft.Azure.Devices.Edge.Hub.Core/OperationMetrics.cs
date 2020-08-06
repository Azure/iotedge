// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    public class OperationMetrics
    {
        readonly IMetricsCounter retriesCounter;

        OperationMetrics()
        {
            this.retriesCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                "operation_retry",
                "Operation retries",
                new List<string> { "id", "operation", MetricsConstants.MsTelemetry });
        }

        public static OperationMetrics Instance { get; } = new OperationMetrics();

        public void LogRetryOperation(long metricValue, string id, string operation)
        {
            this.retriesCounter.Increment(metricValue, new[] { id, operation, bool.TrueString });
        }
    }
}
