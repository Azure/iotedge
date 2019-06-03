// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System.Collections.Generic;

    public class NullMetricsCounter : IMetricsCounter
    {
        public void Increment(long amount)
        {
        }

        public void Decrement(long amount)
        {
        }

        public void Increment(long amount, Dictionary<string, string> tags)
        {
        }

        public void Decrement(long amount, Dictionary<string, string> tags)
        {
        }
    }
}
