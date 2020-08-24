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
            IEnumerable<Metric> metrics = Enumerable.Range(1, 10).Select(i => new Metric(now, "test_metric", i, new Dictionary<string, string> { { "key1", i.ToString() } }));

            Metric result = aggrigator.AggrigateMetrics(metrics).Single();
            Assert.Equal(55, result.Value); // should be sum of 1-10
        }

        [Fact]
        public void TestKeepsNonAggrigateTagsSeperate()
        {
            MetricAggrigator aggrigator = new MetricAggrigator(("key1", (double x, double y) => x + y));

            DateTime now = DateTime.UtcNow;
            IEnumerable<Metric> metrics = Enumerable.Range(1, 10).Select(i => new Metric(now, "test_metric", i, new Dictionary<string, string> { { "key1", i.ToString() }, { "key2", (i % 2).ToString() } }));

            Metric[] results = aggrigator.AggrigateMetrics(metrics).ToArray();

            Assert.Equal(2 + 4 + 6 + 8 + 10, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "0"))).Single().Value);
            Assert.Equal(1 + 3 + 5 + 7 + 9, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "1"))).Single().Value);
        }

        [Fact]
        public void TestMultipleAggrigation()
        {
            DateTime now = DateTime.UtcNow;
            IEnumerable<Metric> metrics = Enumerable.Range(1, 12).Select(i => new Metric(now, "test_metric", i, new Dictionary<string, string> { { "key1", (i % 2).ToString() }, { "key2", (i % 4).ToString() } })).ToArray();

            // key1, then key2
            MetricAggrigator aggrigator = new MetricAggrigator(
               ("key1", (double x, double y) => x + y),
               ("key2", (double x, double y) => x * y));

            Metric result = aggrigator.AggrigateMetrics(metrics).Single();

            double expected = (1 + 5 + 9) * (2 + 6 + 10) * (3 + 7 + 11) * (4 + 8 + 12);
            Assert.Equal(expected, result.Value);

            // key2, then key1
            aggrigator = new MetricAggrigator(
                ("key2", (double x, double y) => x * y),
                ("key1", (double x, double y) => x + y));

            result = aggrigator.AggrigateMetrics(metrics).Single();

            expected = (1 * 3 * 5 * 7 * 9 * 11) + (2 * 4 * 6 * 8 * 10 * 12);
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public void TestMultipleTagsKeepsNonAggrigateTagsSeperate()
        {
            MetricAggrigator aggrigator = new MetricAggrigator(
                          ("key1", (double x, double y) => x + y),
                          ("key2", (double x, double y) => x * y));

            DateTime now = DateTime.UtcNow;
            IEnumerable<Metric> metrics = Enumerable.Range(1, 12).Select(i => new Metric(now, "test_metric", i, new Dictionary<string, string>
            {
                { "key1", (i % 2).ToString() },
                { "key2", (i % 4).ToString() },
                { "key3", (i <= 6).ToString() }
            })).ToArray();

            Metric[] results = aggrigator.AggrigateMetrics(metrics).ToArray();

            double expected = (1 + 5) * (2 + 6) * 3 * 4;
            Assert.Equal(expected, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", true.ToString()))).Single().Value);

            expected = 9 * 10 * (7 + 11) * (8 + 12);
            Assert.Equal(expected, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", false.ToString()))).Single().Value);
        }
    }
}
