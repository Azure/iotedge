// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// Provides a way to serialize a group of metrics that share a name and tag.
    /// </summary>
    public static class MetricsSerializer
    {
        public static IEnumerable<byte> MetricsToBytes(IEnumerable<Metric> metrics)
        {
            return metrics.GroupBy(m => m.GetMetricKey()).SelectMany(x => MetricGroupsToBytes(
                x.First().Name,
                x.First().Tags,
                x.Select(m => new RawMetricValue(m.TimeGeneratedUtc, m.Value))));
        }

        static IEnumerable<byte> MetricGroupsToBytes(string name, string tags, IEnumerable<RawMetricValue> rawValues)
        {
            byte[] nameArray = Encoding.UTF8.GetBytes(name);
            byte[] nameLength = BitConverter.GetBytes(name.Length);

            byte[] tagsArray = Encoding.UTF8.GetBytes(tags);
            byte[] tagsLength = BitConverter.GetBytes(tags.Length);

            // Unfortunately this extra array is necessary, since we need to know the length before we enumerate the values.
            RawMetricValue[] rawValuesArray = rawValues.ToArray();
            byte[] valuesLength = BitConverter.GetBytes(rawValuesArray.Length);
            IEnumerable<byte> values = RawMetricValue.RawValuesToBytes(rawValuesArray);

            return nameLength.Concat(nameArray).Concat(tagsLength).Concat(tagsArray).Concat(valuesLength).Concat(values);
        }

        public static IEnumerable<Metric> BytesToMetrics(byte[] bytes)
        {
            int index = 0;
            while (index < bytes.Length)
            {
                string name, tags;
                IEnumerable<RawMetricValue> rawValues;
                try
                {
                    int nameLength = BitConverter.ToInt32(bytes, index);
                    index = checked(index + sizeof(int));
                    name = Encoding.UTF8.GetString(bytes, index, nameLength);
                    index = checked(index + nameLength);

                    int tagsLength = BitConverter.ToInt32(bytes, index);
                    index = checked(index + sizeof(int));
                    tags = Encoding.UTF8.GetString(bytes, index, tagsLength);
                    index = checked(index + tagsLength);

                    int valuesLength = BitConverter.ToInt32(bytes, index);
                    index = checked(index + sizeof(int));

                    int oldIndex = index;
                    index = checked(index + valuesLength * RawMetricValue.EncodedSize);
                    rawValues = RawMetricValue.BytesToRawValues(bytes, oldIndex, valuesLength);
                }
                catch (Exception e) when (e is OverflowException || e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new InvalidDataException("Error decoding metrics", e);
                }

                foreach (RawMetricValue rawValue in rawValues)
                {
                    yield return new Metric(
                        rawValue.TimeGeneratedUtc,
                        name,
                        rawValue.Value,
                        tags);
                }
            }
        }
    }

    /// <summary>
    /// Provides a way to serialize the datetime and value component of a metric.
    /// </summary>
    public class RawMetricValue : IEquatable<RawMetricValue>
    {
        // The size of the time and value when converted to raw bytes.
        public const int EncodedSize = sizeof(long) + sizeof(double);

        public RawMetricValue(DateTime timeGeneratedUtc, double value)
        {
            Preconditions.CheckArgument(timeGeneratedUtc.Kind == DateTimeKind.Utc);
            this.TimeGeneratedUtc = timeGeneratedUtc;
            this.Value = value;
        }

        public DateTime TimeGeneratedUtc { get; }
        public double Value { get; }

        public static IEnumerable<byte> RawValuesToBytes(IEnumerable<RawMetricValue> rawValues)
        {
            return rawValues.SelectMany(t => BitConverter.GetBytes(t.TimeGeneratedUtc.Ticks).Concat(BitConverter.GetBytes(t.Value)));
        }

        public static IEnumerable<RawMetricValue> BytesToRawValues(byte[] bytes, int startIndex = 0, int length = 1)
        {
            int stop = startIndex + length * EncodedSize;
            while (startIndex < stop)
            {
                long ticks;
                double value;
                try
                {
                    ticks = BitConverter.ToInt64(bytes, startIndex);
                    startIndex += sizeof(long);
                    value = BitConverter.ToDouble(bytes, startIndex);
                    startIndex += sizeof(double);
                }
                catch (Exception e) when (e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new InvalidDataException("Error decoding metrics", e);
                }

                yield return new RawMetricValue(new DateTime(ticks, DateTimeKind.Utc), value);
            }
        }

        public bool Equals(RawMetricValue other)
        {
            return this.TimeGeneratedUtc == other.TimeGeneratedUtc &&
                this.Value == other.Value;
        }
    }
}
