// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMetricsProvider
    {
        IMetricsCounter CreateCounter(string name, string description, List<string> labelNames);

        IMetricsGauge CreateGauge(string name, string description, List<string> labelNames);

        IMetricsTimer CreateTimer(string name, string description, List<string> labelNames);

        IMetricsHistogram CreateHistogram(string name, string description, List<string> labelNames);

        IMetricsDuration CreateDuration(string name, string description, List<string> labelNames);

        Task<byte[]> GetSnapshot(CancellationToken cancellationToken);
    }
}
