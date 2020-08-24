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
        DateTime now = DateTime.UtcNow;

        [Fact]
        public void TestBasicFunctionality()
        {
            MetricAggrigator aggrigator = new MetricAggrigator(("key1", (double x, double y) => x + y));

            // metrics with 1 tag, key1, that has key values of val[1-10]. The key values don't matter for this test and are ignored by the aggregator. Only the metric value (1-10) is summed.
            IEnumerable<Metric> metrics = Enumerable.Range(1, 10).Select(i => new Metric(this.now, "test_metric", i, new Dictionary<string, string> { { "key1", $"val{i}" } }));

            Metric result = aggrigator.AggrigateMetrics(metrics).Single();
            Assert.Equal(55, result.Value); // should be sum of 1-10
        }

        [Fact]
        public void TestKeepsNonAggrigateTagsSeperate()
        {
            MetricAggrigator aggrigator = new MetricAggrigator(("key1", (double x, double y) => x + y));

            // metrics with 2 tags, key1, that has key values of val[1-10], and key2, which has key values val[0-1]. The values are summed by shared key2, and key1's value is ignored.
            IEnumerable<Metric> metrics = Enumerable.Range(1, 10).Select(i => new Metric(this.now, "test_metric", i, new Dictionary<string, string>
            {
                { "key1", $"val{i}" },
                { "key2", $"val{i % 2}" }
            }));

            Metric[] results = aggrigator.AggrigateMetrics(metrics).ToArray();

            Assert.Equal(2 + 4 + 6 + 8 + 10, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val0"))).Single().Value);
            Assert.Equal(1 + 3 + 5 + 7 + 9, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val1"))).Single().Value);
        }

        [Fact]
        public void TestKeepsMultipleNonAggrigateTagsSeperate()
        {
            MetricAggrigator aggrigator = new MetricAggrigator(("key1", (double x, double y) => x + y));

            // metrics with 3 tags, key1, that has key values of val[1-12], key2, which has key values val[0-1], and key3, which has values val[0-2]. The values are summed by shared key2 and key3, and key1's value is ignored.
            IEnumerable<Metric> metrics = Enumerable.Range(1, 12).Select(i => new Metric(this.now, "test_metric", i, new Dictionary<string, string>
            {
                { "key1", $"val{i}" },
                { "key2", $"val{i % 2}" },
                { "key3", $"val{i % 3}" }
            }));

            Metric[] results = aggrigator.AggrigateMetrics(metrics).ToArray();

            Assert.Equal(6 + 12, results
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val0")))
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", "val0")))
                .Single().Value);

            Assert.Equal(3 + 9, results
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val1")))
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", "val0")))
                .Single().Value);

            Assert.Equal(4 + 10, results
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val0")))
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", "val1")))
                .Single().Value);

            Assert.Equal(1 + 7, results
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val1")))
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", "val1")))
                .Single().Value);

            Assert.Equal(2 + 8, results
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val0")))
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", "val2")))
                .Single().Value);

            Assert.Equal(5 + 11, results
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key2", "val1")))
                .Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", "val2")))
                .Single().Value);
        }

        [Fact]
        public void TestMultipleAggrigation()
        {
            // metrics with 2 tags, key1, that has key values of val[0-1], and key2, which has key values of val[0-3].
            IEnumerable<Metric> metrics = Enumerable.Range(1, 12).Select(i => new Metric(this.now, "test_metric", i, new Dictionary<string, string>
            {
                { "key1", $"val{i % 2}" },
                { "key2", $"val{i % 4}" }
            })).ToArray();

            // values are summed by ignoring key1 first (so shared key2), then multiplied together (since only key 2 is left)
            MetricAggrigator aggrigator = new MetricAggrigator(
               ("key1", (double x, double y) => x + y),
               ("key2", (double x, double y) => x * y));

            Metric result = aggrigator.AggrigateMetrics(metrics).Single();

            // split by key2 (mod 4) and summed, then the result is multiplied
            double expected = (1 + 5 + 9) * (2 + 6 + 10) * (3 + 7 + 11) * (4 + 8 + 12);
            Assert.Equal(expected, result.Value);

            // values are multiplied by ignoring key2 first (so shared key1), then summed together (since only key1 is left)
            aggrigator = new MetricAggrigator(
                ("key2", (double x, double y) => x * y),
                ("key1", (double x, double y) => x + y));

            result = aggrigator.AggrigateMetrics(metrics).Single();

            // split by key1 (mod 2) and multiplied, then the result is summed
            expected = (1 * 3 * 5 * 7 * 9 * 11) + (2 * 4 * 6 * 8 * 10 * 12);
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public void TestMultipleTagsKeepsNonAggrigateTagsSeperate()
        {
            MetricAggrigator aggrigator = new MetricAggrigator(
                          ("key1", (double x, double y) => x + y),
                          ("key2", (double x, double y) => x * y));

            // metrics with 3 tags, key1, that has key values of val[0-1], key2, which has key values of val[0-3], and key3, which has values [True/False].
            IEnumerable<Metric> metrics = Enumerable.Range(1, 16).Select(i => new Metric(this.now, "test_metric", i, new Dictionary<string, string>
            {
                { "key1", $"val{i % 2}" },
                { "key2", $"val{i % 4}" },
                { "key3", (i <= 8).ToString() }
            })).ToArray();

            Metric[] results = aggrigator.AggrigateMetrics(metrics).ToArray();

            // always split by key3 (<= 8), then split by key2 (mod 4) and summed, then multiplied
            double expected = (1 + 5) * (2 + 6) * (3 + 7) * (4 + 8);
            Assert.Equal(expected, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", true.ToString()))).Single().Value);

            expected = (9 + 13) * (10 + 14) * (11 + 15) * (12 + 16);
            Assert.Equal(expected, results.Where(m => m.Tags.Contains(new KeyValuePair<string, string>("key3", false.ToString()))).Single().Value);
        }
    }
}
