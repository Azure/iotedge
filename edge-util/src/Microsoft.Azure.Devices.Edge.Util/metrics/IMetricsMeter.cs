// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Collections.Generic;

    public interface IMetricsMeter
    {
        void Mark();
        void Mark(long count);
        void Mark(Dictionary<string, string> tags);
        void Mark(long count, Dictionary<string, string> tags);
    }
}
