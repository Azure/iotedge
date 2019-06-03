// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMetricsProvider
    {
        IMetricsCounter CreateCounter(string name, Dictionary<string, string> tags);

        IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags);

        IMetricsMeter CreateMeter(string name, Dictionary<string, string> defaultTags);

        IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags);

        IMetricsHistogram CreateHistogram(string name, Dictionary<string, string> defaultTags);

        Task<byte[]> GetSnapshot(CancellationToken cancellationToken);
    }
}
