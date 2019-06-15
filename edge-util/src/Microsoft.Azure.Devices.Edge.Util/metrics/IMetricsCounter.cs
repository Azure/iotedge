// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    public interface IMetricsCounter
    {
        void Increment(string[] labelValues);
    }
}
