// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util.Aggrigation
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class AggrigationTemplate
    {
        public string Name { get; }

        public (string tag, IAggrigator aggrigator)[] TagsToAggrigate { get; }

        public AggrigationTemplate(string name, params (string tag, IAggrigator aggrigator)[] tagsToAggrigate)
        {
            this.Name = name;
            this.TagsToAggrigate = tagsToAggrigate;
        }
    }
}
