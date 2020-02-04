// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    public interface IMetricsDuration
    {
        void Set(double value, string[] labelValues);
    }
}
