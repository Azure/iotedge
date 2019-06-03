// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Collections.Generic;

    public interface IMetricsGauge
    {
        void Set(long value);
        void Set(long value, Dictionary<string, string> tags);
    }
}
