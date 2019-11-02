// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Provides a way to serialize the datetime and value component of a metric.
    /// </summary>
    public class RawValue
    {
        public const int Size = sizeof(long) + sizeof(double);
        public DateTime TimeGeneratedUtc { get; set; }
        public double Value { get; set; }

        public static IEnumerable<byte> RawValuesToBytes(IEnumerable<RawValue> rawValues)
        {
            return rawValues.SelectMany(t => BitConverter.GetBytes(t.TimeGeneratedUtc.Ticks).Concat(BitConverter.GetBytes(t.Value)));
        }

        public static IEnumerable<RawValue> BytesToRawValues(byte[] bytes, int startIndex = 0, int length = 1)
        {
            int stop = startIndex + length * Size;
            while (startIndex < stop)
            {
                long ticks = BitConverter.ToInt64(bytes, startIndex);
                startIndex += sizeof(long);
                double value = BitConverter.ToDouble(bytes, startIndex);
                startIndex += sizeof(double);

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
        public static IEnumerable<byte> MetricsToBytes(IEnumerable<Metric> metrics)
        {
            return metrics.GroupBy(m => m.GetValuelessHash()).SelectMany(x => MetricGroupsToBytes(
                x.First().Name,
                x.First().Tags,
                x.Select(m => new RawValue
                {
                    TimeGeneratedUtc = m.TimeGeneratedUtc,
                    Value = m.Value
                })));
        }

        static IEnumerable<byte> MetricGroupsToBytes(string name, string tags, IEnumerable<RawValue> rawValues)
        {
            byte[] nameArray = Encoding.UTF8.GetBytes(name);
            byte[] nameLength = BitConverter.GetBytes(name.Length);

            byte[] tagsArray = Encoding.UTF8.GetBytes(tags);
            byte[] tagsLength = BitConverter.GetBytes(tags.Length);

            // Unfortunately this extra array is necessary, since we need to know the length before we enumerate the values.
            RawValue[] rawValuesArray = rawValues.ToArray();
            byte[] valuesLength = BitConverter.GetBytes(rawValuesArray.Length);
            IEnumerable<byte> values = RawValue.RawValuesToBytes(rawValuesArray);

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
                index += valuesLength * RawValue.Size;

                foreach (RawValue rawValue in rawValues)
                {
                    yield return new Metric(
                        rawValue.TimeGeneratedUtc,
                        "prometheous",
                        name,
                        rawValue.Value,
                        tags);
                }
            }
        }
    }
}
