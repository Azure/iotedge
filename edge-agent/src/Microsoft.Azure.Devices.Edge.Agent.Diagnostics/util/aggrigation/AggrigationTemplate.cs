// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggrigation
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class AggrigationTemplate
    {
        public IEnumerable<string> TargetMetricNames { get; }

        public (string targetTag, IAggrigator aggrigator)[] TagsToAggrigate { get; }

        public AggrigationTemplate(string targetName, string targetTag, IAggrigator aggrigator)
            : this(new string[] { targetName }, (targetTag, aggrigator))
        {
        }

        public AggrigationTemplate(string targetName, params (string targetTag, IAggrigator aggrigator)[] tagsToAggrigate)
            : this(new string[] { targetName }, tagsToAggrigate)
        {
        }

        public AggrigationTemplate(IEnumerable<string> targeMetricNames, params (string targetTag, IAggrigator aggrigator)[] tagsToAggrigate)
        {
            this.TargetMetricNames = targeMetricNames;
            this.TagsToAggrigate = tagsToAggrigate;
        }
    }
}
