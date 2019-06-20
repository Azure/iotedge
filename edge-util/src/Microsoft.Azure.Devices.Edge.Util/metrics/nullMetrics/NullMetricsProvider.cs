// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class NullMetricsProvider : IMetricsProvider
    {
        public IMetricsCounter CreateCounter(string name, string description, List<string> labelNames)
            => new NullMetricsCounter();

        public IMetricsGauge CreateGauge(string name, string description, List<string> labelNames)
            => new NullMetricsGauge();

        public IMetricsHistogram CreateHistogram(string name, string description, List<string> labelNames)
            => new NullMetricsHistogram();

        public IMetricsTimer CreateTimer(string name, string description, List<string> labelNames)
            => new NullMetricsTimer();

        public Task<byte[]> GetSnapshot(CancellationToken cancellationToken)
            => Task.FromResult(new byte[0]);

        public IMetricsDuration CreateDuration(string name, string description, List<string> labelNames)
            => new NullMetricsDuration();
    }
}
