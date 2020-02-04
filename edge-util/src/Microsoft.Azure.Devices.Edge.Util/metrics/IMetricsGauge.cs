// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    public interface IMetricsGauge
    {
        void Set(double value, string[] labelValues);
    }
}
