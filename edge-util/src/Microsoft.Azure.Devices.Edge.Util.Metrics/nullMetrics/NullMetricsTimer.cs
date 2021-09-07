// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System;

    public class NullMetricsTimer : IMetricsTimer
    {
        public IDisposable GetTimer(string[] labelValues) => NullDisposable.Instance;
    }
}
