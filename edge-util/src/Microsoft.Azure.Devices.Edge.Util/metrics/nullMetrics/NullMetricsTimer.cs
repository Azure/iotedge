// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System;
    using System.Collections.Generic;

    public class NullMetricsTimer : IMetricsTimer
    {
        public IDisposable GetTimer() => NullDisposable.Instance;

        public IDisposable GetTimer(Dictionary<string, string> tags) => NullDisposable.Instance;
    }
}
