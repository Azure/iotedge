// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggregation
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class AggregationTemplate
    {
        public IEnumerable<string> TargetMetricNames { get; }

        public (string targetTag, IAggregator aggregator)[] TagsToAggrigate { get; }

        public AggregationTemplate(string targetName, string targetTag, IAggregator aggregator)
            : this(new string[] { targetName }, (targetTag, aggregator))
        {
        }

        public AggregationTemplate(string targetName, params (string targetTag, IAggregator aggregator)[] tagsToAggrigate)
            : this(new string[] { targetName }, tagsToAggrigate)
        {
        }

        public AggregationTemplate(IEnumerable<string> targeMetricNames, string targetTag, IAggregator aggregator)
            : this(targeMetricNames, (targetTag, aggregator))
        {
        }

        public AggregationTemplate(IEnumerable<string> targeMetricNames, params (string targetTag, IAggregator aggregator)[] tagsToAggrigate)
        {
            this.TargetMetricNames = targeMetricNames;
            this.TagsToAggrigate = tagsToAggrigate;
        }
    }
}
