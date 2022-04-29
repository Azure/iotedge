// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;

    public interface IMetricsTimer
    {
        IDisposable GetTimer(string[] labelValues);
    }
}
