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

        public readonly Lazy<int> MetricKey;

        public Metric(DateTime timeGeneratedUtc, string name, double value, IReadOnlyDictionary<string, string> tags)
        {
            Preconditions.CheckArgument(timeGeneratedUtc.Kind == DateTimeKind.Utc, $"Metric {nameof(timeGeneratedUtc)} parameter only supports in UTC.");
            this.TimeGeneratedUtc = timeGeneratedUtc;
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Value = value;
            this.Tags = Preconditions.CheckNotNull(tags, nameof(tags));

            this.MetricKey = new Lazy<int>(() => this.GetMetricKey());
        }

        public bool Equals(Metric other)
        {
            return this.TimeGeneratedUtc == other.TimeGeneratedUtc &&
                this.Name == other.Name &&
                this.Value == other.Value &&
                GetOrderIndependentHash(this.Tags) == GetOrderIndependentHash(other.Tags);
        }

        /// <summary>
        /// This combines the hashes of name and tags to make it easy to group all metrics.
        /// </summary>
        /// <returns>Hash of name and tags.</returns>
        int GetMetricKey()
        {
            return HashCode.Combine(this.Name.GetHashCode(), GetOrderIndependentHash(this.Tags));
        }

        static int GetOrderIndependentHash<T1, T2>(IEnumerable<KeyValuePair<T1, T2>> dictionary)
        {
            return dictionary.Select(o => HashCode.Combine(o.Key.GetHashCode(), o.Value.GetHashCode())).OrderBy(h => h).Aggregate(0, HashCode.Combine);
        }
    }
}
