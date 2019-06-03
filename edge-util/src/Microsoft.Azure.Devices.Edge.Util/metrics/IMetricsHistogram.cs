// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Collections.Generic;

    public interface IMetricsHistogram
    {
        void Update(long value);
        void Update(long value, Dictionary<string, string> tags);
    }
}
