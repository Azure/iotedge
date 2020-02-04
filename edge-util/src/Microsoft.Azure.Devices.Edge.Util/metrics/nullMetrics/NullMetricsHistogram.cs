// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    public class NullMetricsHistogram : IMetricsHistogram
    {
        public void Update(double value, string[] labelValues)
        {
        }
    }
}
