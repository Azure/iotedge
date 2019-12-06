// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class Metric : IEquatable<Metric>
    {
        public DateTime TimeGeneratedUtc { get; }
        public string Name { get; }
        public double Value { get; }
        public string Tags { get; }

        public Metric(DateTime timeGeneratedUtc, string name, double value, string tags)
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
            // TODO: replace with "return HashCode.Combine(Name.GetHashCode(), Tags.GetHashCode());"
            // when upgraded to .net standard 2.1: https://docs.microsoft.com/en-us/dotnet/api/system.hashcode.combine?view=netstandard-2.1
            int hash = 17;
            hash = hash * 31 + this.Name.GetHashCode();
            hash = hash * 31 + this.Tags.GetHashCode();

            return hash;
        }

        public bool Equals(Metric other)
        {
            return this.TimeGeneratedUtc == other.TimeGeneratedUtc &&
                this.Name == other.Name &&
                this.Value == other.Value &&
                this.Tags == other.Tags;
        }
    }
}
