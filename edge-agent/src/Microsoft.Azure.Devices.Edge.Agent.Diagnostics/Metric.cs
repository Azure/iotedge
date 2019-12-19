// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class Metric : IEquatable<Metric>
    {
        public DateTime TimeGeneratedUtc { get; }
        public string Name { get; }
        public double Value { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }

        int? metricKey;

        public Metric(DateTime timeGeneratedUtc, string name, double value, IReadOnlyDictionary<string, string> tags)
        {
            Preconditions.CheckArgument(timeGeneratedUtc.Kind == DateTimeKind.Utc, $"Metric {nameof(timeGeneratedUtc)} parameter only supports in UTC.");
            this.TimeGeneratedUtc = timeGeneratedUtc;
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Value = value;
            this.Tags = Preconditions.CheckNotNull(tags, nameof(tags));
        }

        /// <summary>
        /// This combines the hashes of name and tags to make it easy to group all metrics.
        /// </summary>
        /// <returns>Hash of name and tags.</returns>
        public int GetMetricKey()
        {
            if (this.metricKey == null)
            {
                this.metricKey = Temp.CombineHash(this.Name, this.Tags.OrderIndependentHash());
            }

            return this.metricKey.Value;
        }

        public bool Equals(Metric other)
        {
            return this.TimeGeneratedUtc == other.TimeGeneratedUtc &&
                this.Name == other.Name &&
                this.Value == other.Value &&
                this.Tags.OrderIndependentHash() == other.Tags.OrderIndependentHash();
        }
    }

    public static class Temp
    {
        public static int OrderIndependentHash<T1, T2>(this IEnumerable<KeyValuePair<T1, T2>> dictionary)
        {
            return CombineHash(dictionary.Select(o => CombineHash(o.Key, o.Value)).OrderBy(h => h));
        }

        // TODO: replace with "return HashCode.Combine(Name.GetHashCode(), Tags.GetHashCode());"
        // when upgraded to .net standard 2.1: https://docs.microsoft.com/en-us/dotnet/api/system.hashcode.combine?view=netstandard-2.1
        public static int CombineHash(params object[] objects)
        {
            int hash = 17;
            foreach (object o in objects)
            {
                hash = hash * 31 + o.GetHashCode();
            }

            return hash;
        }
    }
}
