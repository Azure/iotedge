// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    public interface IMetricsGauge
    {
        double Get(string[] labelValues);

        void Set(double value, string[] labelValues);

        void Increment(string[] labelValues);

        void Decrement(string[] labelValues);
    }
}
