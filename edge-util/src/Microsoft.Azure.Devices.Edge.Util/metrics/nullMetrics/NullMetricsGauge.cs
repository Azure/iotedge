// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    public class NullMetricsGauge : IMetricsGauge
    {
        public double Get(string[] labelValues) => 0;

        public void Set(double value, string[] labelValues)
        {
        }

        public void Increment(string[] labelValues)
        {
        }

        public void Decrement(string[] labelValues)
        {
        }
    }
}
