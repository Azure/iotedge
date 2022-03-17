// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Diagnostics.Contracts;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class Metric : IEquatable<Metric>
    {
        public DateTime TimeGeneratedUtc { get; }
        public string Name { get; }
        public double Value { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }

        public string Endpoint { get; }

        public Metric(DateTime timeGeneratedUtc, string name, double value, IReadOnlyDictionary<string, string> tags)
        {
            Preconditions.CheckArgument(timeGeneratedUtc.Kind == DateTimeKind.Utc, $"Metric {nameof(timeGeneratedUtc)} parameter only supports in UTC.");
            this.TimeGeneratedUtc = timeGeneratedUtc;
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.Value = value;
            this.Tags = Preconditions.CheckNotNull(tags, nameof(tags));
        }

        public Metric(DateTime timeGeneratedUtc, string name, double value, IReadOnlyDictionary<string, string> tags, string endpoint) : this(timeGeneratedUtc, name, value, tags)
        {
            Contract.Requires(endpoint != null);
            this.Endpoint = endpoint;
        }

        /// <summary>
        /// Checks for equality between Metric objects (as one would expect)
        /// Pedantic Note: Metric values are checked for exact equality (with no error tolerance). This may cause problems if doing math on metric
        /// values or comparing metrics from different sources
        /// </summary>
        public bool Equals(Metric other)
        {
            return this.TimeGeneratedUtc == other.TimeGeneratedUtc &&
                this.Name == other.Name &&
                (this.Value == other.Value || (double.IsNaN(this.Value) && double.IsNaN(other.Value))) &&  // (NaN == NaN evaluates to false)
                GetOrderIndependentHash(this.Tags) == GetOrderIndependentHash(other.Tags);
        }

        public override bool Equals(Object other)
        {
            if (other == null)
                return false;

            Object metricObj = other as Metric;
            if (metricObj == null)
                return false;
            else
                return Equals(metricObj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(new int[]{this.TimeGeneratedUtc.GetHashCode(),
                                              this.Name.GetHashCode(),
                                              this.Value.GetHashCode(),
                                              GetOrderIndependentHash(this.Tags)});
        }

        public static bool operator == (Metric metric1, Metric metric2)
        {
            if (((object)metric1) == null || ((object) metric2) == null)
                return Object.Equals(metric1, metric2);

            return metric1.Equals(metric2);
        }

        public static bool operator != (Metric metric1, Metric metric2)
        {
            if (((object)metric1) == null || ((object) metric2) == null)
                return ! Object.Equals(metric1, metric2);

            return ! metric1.Equals(metric2);
        }

        private static int GetOrderIndependentHash<T1, T2>(IEnumerable<KeyValuePair<T1, T2>> dictionary)
        {
            return CombineHash(dictionary.Select(o => CombineHash(o.Key.GetHashCode(), o.Value.GetHashCode())).OrderBy(h => h).ToArray());
        }

        private static int CombineHash(params int[] hashes)
        {
            // For some reason using HashCode.Combine() makes GetOrderIndependentHash nondeterministic (but the manual 
            // hash combining below works). Maybe someone with more c# experience knows why?
            // return HashCode.Combine(hashes);

            int hash = 17;
            foreach (int h in hashes)
            {
                hash = hash * 31 + h;
            }

            return hash;
        }
    }
}
