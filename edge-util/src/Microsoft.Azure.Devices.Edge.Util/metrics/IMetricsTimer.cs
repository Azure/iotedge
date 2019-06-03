// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Collections.Generic;

    public interface IMetricsTimer
    {
        IDisposable GetTimer();
        IDisposable GetTimer(Dictionary<string, string> tags);
    }
}
