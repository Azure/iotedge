// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class IoTHubMetricsUpload : IMetricsUpload
    {
        readonly ModuleClient moduleClient;

        public IoTHubMetricsUpload(ModuleClient moduleClient)
        {
            this.moduleClient = moduleClient;
        }

        public async Task UploadAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            IEnumerable<byte> data = metrics.GroupBy(m => m.GetValuelessHash()).SelectMany(x => RawMetric.MetricGroupsToBytes(
                x.First().Name,
                x.First().Tags,
                x.Select(m => new RawValue
                {
                    TimeGeneratedUtc = m.TimeGeneratedUtc,
                    Value = m.Value
                }).ToArray()));

            Message message = new Message(data.ToArray());

            // TODO: add check for too big of a message
            await this.moduleClient.SendEventAsync(message);
        }
    }

    /// <summary>
    /// Provides a way to serialize the datetime and value component of a metric.
    /// </summary>
    public class RawValue
    {
        public DateTime TimeGeneratedUtc { get; set; }
        public double Value { get; set; }

        public static IEnumerable<byte> RawValuesToBytes(IEnumerable<RawValue> rawValues)
        {
            return rawValues.SelectMany(t => BitConverter.GetBytes(t.TimeGeneratedUtc.Ticks).Concat(BitConverter.GetBytes(t.Value)));
        }

        public static IEnumerable<RawValue> BytesToRawValues(byte[] bytes, int start, int length)
        {
            int index = start;
            while (index < start + length)
            {
                long ticks = BitConverter.ToInt64(bytes, index);
                index += sizeof(long);
                double value = BitConverter.ToDouble(bytes, index);
                index += sizeof(double);

                yield return new RawValue
                {
                    TimeGeneratedUtc = new DateTime(ticks),
                    Value = value
                };
            }
        }
    }

    /// <summary>
    /// Provides a way to serialize a group of metrics that share a name and tag.
    /// </summary>
    public static class RawMetric
    {
        public static IEnumerable<byte> MetricGroupsToBytes(string name, string tags, RawValue[] rawValues)
        {
            byte[] nameArray = Encoding.UTF8.GetBytes(name);
            byte[] nameLength = BitConverter.GetBytes(name.Length);

            byte[] tagsArray = Encoding.UTF8.GetBytes(tags);
            byte[] tagsLength = BitConverter.GetBytes(tags.Length);

            byte[] valuesLength = BitConverter.GetBytes(rawValues.Length);
            IEnumerable<byte> values = RawValue.RawValuesToBytes(rawValues);

            return nameLength.Concat(nameArray).Concat(tagsLength).Concat(tagsArray).Concat(valuesLength).Concat(values);
        }

        public static IEnumerable<Metric> BytesToMetrics(byte[] bytes)
        {
            int index = 0;
            while (index < bytes.Length)
            {
                int nameLength = BitConverter.ToInt32(bytes, index);
                index += sizeof(int);
                string name = Encoding.UTF8.GetString(bytes, index, nameLength);
                index += nameLength;

                int tagsLength = BitConverter.ToInt32(bytes, index);
                index += sizeof(int);
                string tags = Encoding.UTF8.GetString(bytes, index, tagsLength);
                index += tagsLength;

                int valuesLength = BitConverter.ToInt32(bytes, index);
                index += sizeof(int);
                IEnumerable<RawValue> rawValues = RawValue.BytesToRawValues(bytes, index, valuesLength);
                index += valuesLength;

                return rawValues.Select(rawValue => new Metric(
                    rawValue.TimeGeneratedUtc,
                    "prometheous",
                    name,
                    rawValue.Value,
                    tags));
            }

            return Enumerable.Empty<Metric>();
        }
    }
}
