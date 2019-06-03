// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Collections.Generic;

    public interface IMetricsCounter
    {
        void Increment(long amount);
        void Decrement(long amount);
        void Increment(long amount, Dictionary<string, string> tags);
        void Decrement(long amount, Dictionary<string, string> tags);
    }
}
