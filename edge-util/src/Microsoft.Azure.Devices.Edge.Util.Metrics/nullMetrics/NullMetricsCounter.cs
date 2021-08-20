// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    public class NullMetricsCounter : IMetricsCounter
    {
        public void Increment(long count, string[] labelValues)
        {
        }
    }
}
