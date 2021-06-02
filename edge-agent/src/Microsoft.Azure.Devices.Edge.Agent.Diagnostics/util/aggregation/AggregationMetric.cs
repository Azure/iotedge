// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggregation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    // This class is used to temporaraly compare similar metrics. It doesn't include values, and
    // the aggregate tag is removed
    public class AggregateMetric
    {
        public DateTime TimeGeneratedUtc { get; }
        public string Name { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }

        Lazy<int> hash;

        public AggregateMetric(Metric metric, string aggregateTag)
        {
            this.Name = metric.Name;
            this.TimeGeneratedUtc = metric.TimeGeneratedUtc;
            this.Tags = new Dictionary<string, string>(metric.Tags.Where(t => t.Key != aggregateTag));

            this.hash = new Lazy<int>(this.Hash);
        }

        public Metric ToMetric(double value)
        {
            return new Metric(this.TimeGeneratedUtc, this.Name, value, this.Tags);
        }

        public override bool Equals(object other)
        {
            return this.GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            return this.hash.Value;
        }

        int Hash()
        {
            return HashCode.Combine(
                this.Name.GetHashCode(),
                this.TimeGeneratedUtc.GetHashCode(),
                this.Tags.Select(o => HashCode.Combine(
                        o.Key.GetHashCode(),
                        o.Value.GetHashCode()))
                    .OrderBy(h => h)
                    .Aggregate(0, HashCode.Combine));
        }
    }
}
