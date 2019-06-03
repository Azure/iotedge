// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Collections.Generic;

    public interface IMetricsProvider
    {
        ICounter CreateCounter(string name, Dictionary<string, string> tags);

        IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags);

        IMetricsMeter CreateMeter(string name, Dictionary<string, string> defaultTags);

        IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags);

        IMetricsHistogram CreateHistogram(string name, Dictionary<string, string> defaultTags);
    }
}
