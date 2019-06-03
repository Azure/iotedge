// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class NullMetricsProvider : IMetricsProvider
    {
        public IMetricsCounter CreateCounter(string name, Dictionary<string, string> tags)
            => new NullMetricsCounter();

        public IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags)
            => new NullMetricsGauge();

        public IMetricsHistogram CreateHistogram(string name, Dictionary<string, string> defaultTags)
            => new NullMetricsHistogram();

        public IMetricsMeter CreateMeter(string name, Dictionary<string, string> defaultTags)
            => new NullMetricsMeter();

        public IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags)
            => new NullMetricsTimer();

        public Task<byte[]> GetSnapshot(CancellationToken cancellationToken)
            => Task.FromResult(new byte[0]);
    }
}
