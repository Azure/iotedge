// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class MetricsSerializerTest
    {
        readonly Random rand = new Random();

        [Fact]
        public void TestSingleMetricSerializes()
        {
            Metric testMetric = new Metric(DateTime.UtcNow, this.metrics[0].name, this.rand.NextDouble() * 50, this.metrics[0].tags);

            byte[] data = MetricsSerializer.MetricsToBytes(new Metric[] { testMetric }).ToArray();
            Metric[] reconstructedValues = MetricsSerializer.BytesToMetrics(data).ToArray();

            Assert.Single(reconstructedValues);
            Assert.Equal(testMetric, reconstructedValues.Single());
        }

        [Fact]
        public void TestSingleMetricSeriesSerializes()
        {
            Metric[] testMetrics = this.GenerateSeries(this.metrics[0].name, this.metrics[0].tags).ToArray();

            byte[] data = MetricsSerializer.MetricsToBytes(testMetrics).ToArray();
            Metric[] reconstructedValues = MetricsSerializer.BytesToMetrics(data).ToArray();

            TestUtilities.OrderlessCompare(testMetrics, reconstructedValues);
        }

        [Fact]
        public void TestMetricsSeriesSerializes()
        {
            Metric[] testMetrics = this.metrics.SelectMany(m => this.GenerateSeries(m.name, m.tags, this.rand.Next(10, 20))).ToArray();

            byte[] data = MetricsSerializer.MetricsToBytes(testMetrics).ToArray();
            Metric[] reconstructedValues = MetricsSerializer.BytesToMetrics(data).ToArray();

            TestUtilities.OrderlessCompare(testMetrics, reconstructedValues);
        }

        [Fact]
        public void TestRawValuesSerialize()
        {
            var time = DateTime.UtcNow;
            RawMetricValue[] testValues = Enumerable.Range(1, 10).Select(i => new RawMetricValue(time.AddHours(i), this.rand.NextDouble() * 200)).ToArray();

            byte[] data = RawMetricValue.RawValuesToBytes(testValues).ToArray();
            RawMetricValue[] reconstructedValues = RawMetricValue.BytesToRawValues(data, 0, testValues.Length).ToArray();

            Assert.Equal(testValues, reconstructedValues);
        }

        [Fact]
        public void TestInvalidDataThrows()
        {
            // Gibberish
            byte[] randData = new byte[300];
            this.rand.NextBytes(randData);
            Assert.Throws<InvalidDataException>(() => MetricsSerializer.BytesToMetrics(randData).ToArray());

            // Overflow
            byte[] overflowData = BitConverter.GetBytes(int.MaxValue).Concat(randData).ToArray();
            var exception = Assert.Throws<InvalidDataException>(() => MetricsSerializer.BytesToMetrics(randData).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => throw exception.InnerException));
        }

        IEnumerable<Metric> GenerateSeries(string name, Dictionary<string, string> tags, int n = 10)
        {
            var time = new DateTime(1000000 * this.rand.Next(1000), DateTimeKind.Utc);
            return Enumerable.Range(1, n).Select(i => new Metric(
                time.AddDays(i + 1.25 * this.rand.NextDouble()),
                name,
                this.rand.NextDouble() * 100,
                tags));
        }

        readonly (string name, Dictionary<string, string> tags)[] metrics =
        {
            ("edgehub_message_size_bytes", new Dictionary<string, string> { { "id", "device4/SimulatedTemperatureSensor" }, { "quantile", "0.5" } }),
            ("edgehub_message_size_bytes", new Dictionary<string, string> { { "id", "device4/SimulatedTemperatureSensor" }, { "quantile", "0.9" } }),
            ("edgehub_message_size_bytes", new Dictionary<string, string> { { "id", "device4/SimulatedTemperatureSensor" }, { "quantile", "0.99" } }),
            ("edgehub_message_size_bytes", new Dictionary<string, string> { { "id", "device4/SimulatedTemperatureSensor" }, { "quantile", "0.999" } }),
            ("edgehub_message_size_bytes", new Dictionary<string, string> { { "id", "device4/SimulatedTemperatureSensor" }, { "quantile", "0.9999" } }),
            ("edgehub_reported_properties_update_duration_seconds", new Dictionary<string, string> { { "id", "device4/$edgeHub" }, { "quantile", "0.5" } }),
            ("edgehub_reported_properties_update_duration_seconds",  new Dictionary<string, string> { { "id", "device4/$edgeHub" }, { "quantile", "0.5" } }),
            ("edgehub_reported_properties_update_duration_seconds", new Dictionary<string, string> { { "id", "device4/$edgeHub" }, { "quantile", "0.5" } }),
            ("edgehub_reported_properties_update_duration_seconds",  new Dictionary<string, string> { { "id", "device4/$edgeHub" }, { "quantile", "0.5" } }),
            ("edgehub_reported_properties_update_duration_seconds",  new Dictionary<string, string> { { "id", "device4/$edgeHub" }, { "quantile", "0.5" } }),
        };
    }
}
