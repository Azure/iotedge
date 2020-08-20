// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Moq;
    using Xunit;

    [Unit]
    public class MetricsAggrigatorTest
    {
        [Fact]
        public void TestBasicFunctionality()
        {
            MetricAggrigator aggrigator = new MetricAggrigator(("key1", (double x, double y) => x + y));

            DateTime now = DateTime.UtcNow;
            IEnumerable<Metric> metrics = Enumerable.Range(1, 10).Select(i => new Metric(now, "test_metric", i, new Dictionary<string, string> { { "key1", "5" } }));

            Metric result = aggrigator.AggrigateMetrics(metrics).Single();
            Assert.Equal(55, result.Value);
        }
    }
}
