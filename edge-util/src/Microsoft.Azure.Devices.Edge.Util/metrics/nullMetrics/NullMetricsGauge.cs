// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System.Collections.Generic;

    public class NullMetricsGauge : IMetricsGauge
    {
        public void Set(long value)
        {
        }

        public void Set(long value, Dictionary<string, string> tags)
        {
        }
    }
}
