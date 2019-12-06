// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class ExtendedMetric : Metric, IEquatable<Metric>
    {
        public IReadOnlyDictionary<string, string> CustomTags { get; }

        public ExtendedMetric(Metric metric, IReadOnlyDictionary<string, string> customTags)
        : base(metric.TimeGeneratedUtc, metric.Name, metric.Value, metric.Tags)
        {
            Preconditions.CheckNotNull(metric);
            this.CustomTags = Preconditions.CheckNotNull(customTags);
        }
    }
}
