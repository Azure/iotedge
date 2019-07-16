// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    public interface IMetricsHistogram
    {
        void Update(long value, string[] labelValues);
    }
}
