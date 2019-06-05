// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System.Collections.Generic;

    public class NullMetricsMeter : IMetricsMeter
    {
        public void Mark()
        {
        }

        public void Mark(long count)
        {
        }

        public void Mark(Dictionary<string, string> tags)
        {
        }

        public void Mark(long count, Dictionary<string, string> tags)
        {
        }
    }
}
