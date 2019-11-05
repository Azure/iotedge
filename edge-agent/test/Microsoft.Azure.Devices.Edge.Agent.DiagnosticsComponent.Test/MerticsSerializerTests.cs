// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class MerticsSerializerTests
    {
        readonly Random rand = new Random();

        [Fact]
        public void SingleMetricSerializes()
        {
            Metric testMetric = new Metric(DateTime.UtcNow, this.metrics[0].name, this.rand.NextDouble() * 50, this.metrics[0].tags);

            byte[] data = RawMetric.MetricsToBytes(new Metric[] { testMetric }).ToArray();
            var reconstructedValues = RawMetric.BytesToMetrics(data).ToArray();

            Assert.Single(reconstructedValues);
            TestUtilities.ReflectionEqual(testMetric, reconstructedValues[0]);
        }

        [Fact]
        public void SingleMetricSeriesSerializes()
        {
            var testMetrics = this.GenerateSeries(this.metrics[0].name, this.metrics[0].tags).ToArray();

            byte[] data = RawMetric.MetricsToBytes(testMetrics).ToArray();
            var reconstructedValues = RawMetric.BytesToMetrics(data).ToArray();

            var expected = testMetrics.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc);
            var actual = reconstructedValues.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc);
            TestUtilities.ReflectionEqualEnumerable(expected, actual);
        }

        [Fact]
        public void MetricsSeriesSerializes()
        {
            var testMetrics = this.metrics.SelectMany(m => this.GenerateSeries(m.name, m.tags, this.rand.Next(10, 20))).ToArray();

            byte[] data = RawMetric.MetricsToBytes(testMetrics).ToArray();
            var reconstructedValues = RawMetric.BytesToMetrics(data).ToArray();

            var expected = testMetrics.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc).ToArray();
            var actual = reconstructedValues.OrderBy(m => m.Name).ThenBy(m => m.Tags).ThenBy(m => m.TimeGeneratedUtc).ToArray();
            TestUtilities.ReflectionEqualEnumerable(expected, actual);
        }

        [Fact]
        public void TestDeflate()
        {
            byte[] data = new byte[10000];
            this.rand.NextBytes(data);

            byte[] compressedData = DeflateSerializer.Compress(data);
            byte[] originalData = DeflateSerializer.Decompress(compressedData);

            Assert.NotEqual(data, compressedData);
            Assert.Equal(data, originalData);
        }

        [Fact]
        public void RawValuesSerialize()
        {
            var time = DateTime.UtcNow;
            var testValues = Enumerable.Range(1, 10).Select(i => new RawMetricValue { TimeGeneratedUtc = time.AddHours(i), Value = this.rand.NextDouble() * 200 }).ToArray();

            byte[] data = RawMetricValue.RawValuesToBytes(testValues).ToArray();
            var reconstructedValues = RawMetricValue.BytesToRawValues(data, 0, testValues.Length).ToArray();

            TestUtilities.ReflectionEqualEnumerable(testValues, reconstructedValues);
        }

        IEnumerable<Metric> GenerateSeries(string name, string tags, int n = 10)
        {
            var time = new DateTime(1000000 * this.rand.Next(1000));
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
