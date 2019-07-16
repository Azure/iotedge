// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    public class NullMetricsDuration : IMetricsDuration
    {
        public void Set(double value, string[] labelValues)
        {
        }
    }
}
