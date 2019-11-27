// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

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
            Assert.Equal(testMetric, reconstructedValues[0]);
        }

        [Fact]
        public void TestSingleMetricSeriesSerializes()
        {
            Metric[] testMetrics = this.GenerateSeries(this.metrics[0].name, this.metrics[0].tags).ToArray();

            byte[] data = MetricsSerializer.MetricsToBytes(testMetrics).ToArray();
            Metric[] reconstructedValues = MetricsSerializer.BytesToMetrics(data).ToArray();

            var expected = testMetrics.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc);
            var actual = reconstructedValues.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestMetricsSeriesSerializes()
        {
            Metric[] testMetrics = this.metrics.SelectMany(m => this.GenerateSeries(m.name, m.tags, this.rand.Next(10, 20))).ToArray();

            byte[] data = MetricsSerializer.MetricsToBytes(testMetrics).ToArray();
            Metric[] reconstructedValues = MetricsSerializer.BytesToMetrics(data).ToArray();

            var expected = testMetrics.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc).ToArray();
            var actual = reconstructedValues.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc).ToArray();
            Assert.Equal(expected, actual);
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

        IEnumerable<Metric> GenerateSeries(string name, string tags, int n = 10)
        {
            var time = new DateTime(1000000 * this.rand.Next(1000), DateTimeKind.Utc);
            return Enumerable.Range(1, n).Select(i => new Metric(
                time.AddDays(i + 1.25 * this.rand.NextDouble()),
                name,
                this.rand.NextDouble() * 100,
                tags));
        }

        readonly (string name, string tags)[] metrics =
        {
            ("edgehub_message_size_bytes", "{id=\"device4/SimulatedTemperatureSensor\",quantile=\"0.5\"}"),
            ("edgehub_message_size_bytes", "{id=\"device4/SimulatedTemperatureSensor\",quantile=\"0.9\"}"),
            ("edgehub_message_size_bytes", "{id=\"device4/SimulatedTemperatureSensor\",quantile=\"0.99\"}"),
            ("edgehub_message_size_bytes", "{id=\"device4/SimulatedTemperatureSensor\",quantile=\"0.999\"}"),
            ("edgehub_message_size_bytes", "{id=\"device4/SimulatedTemperatureSensor\",quantile=\"0.9999\"}"),
            ("edgehub_reported_properties_update_duration_seconds", "{id=\"device4/$edgeHub\",quantile=\"0.5\"}"),
            ("edgehub_reported_properties_update_duration_seconds", "{id=\"device4/$edgeHub\",quantile=\"0.9\"}"),
            ("edgehub_reported_properties_update_duration_seconds", "{id=\"device4/$edgeHub\",quantile=\"0.99\"}"),
            ("edgehub_reported_properties_update_duration_seconds", "{id=\"device4/$edgeHub\",quantile=\"0.999\"}"),
            ("edgehub_reported_properties_update_duration_seconds", "{id=\"device4/$edgeHub\",quantile=\"0.9999\"}"),
            ("edgehub_message_send_duration_seconds_sum", "{\"edge_device\":\"device4\",\"from\":\"device4/SimulatedTemperatureSensor\",\"to\":\"upstream\"}"),
            ("edgehub_message_send_duration_seconds_count", "{\"edge_device\":\"device4\",\"from\":\"device4/SimulatedTemperatureSensor\",\"to\":\"upstream\"}"),
            ("edgehub_message_send_duration_seconds", "{\"edge_device\":\"device4\",\"from\":\"device4/SimulatedTemperatureSensor\",\"to\":\"upstream\",\"quantile\":\"0.9\"}"),
            ("edgehub_messages_received_total", "{\"edge_device\":\"device4\",\"protocol\":\"amqp\",\"id\":\"device4/SimulatedTemperatureSensor\"}"),
            ("edgehub_message_size_bytes_sum", "{\"edge_device\":\"device4\",\"id\":\"device4/SimulatedTemperatureSensor\"}"),
            ("edgehub_message_size_bytes_count", "{\"edge_device\":\"device4\",\"id\":\"device4/SimulatedTemperatureSensor\"}"),
            ("edgehub_reported_properties_total", "{\"edge_device\":\"device4\",\"target\":\"upstream\",\"id\":\"device4/$edgeHub\"}"),
            ("edgehub_messages_sent_total", "{\"edge_device\":\"device4\",\"from\":\"device4/SimulatedTemperatureSensor\",\"to\":\"upstream\"}"),
        };
    }
}
