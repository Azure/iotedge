// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Collections.Generic;

    public class BaseMetric
    {
        readonly string[] defaultLabelValues;

        protected BaseMetric(List<string> labelNames, List<string> defaultLabelValues)
        {
            this.defaultLabelValues = Preconditions.CheckNotNull(defaultLabelValues, nameof(defaultLabelValues)).ToArray();
            this.LabelNames = Preconditions.CheckNotNull(labelNames, nameof(labelNames)).ToArray();
        }

        protected string[] LabelNames { get; }

        protected string[] GetLabelValues(string[] labelValues)
        {
            var labels = new string[this.LabelNames.Length];
            Array.Copy(this.defaultLabelValues, labels, this.defaultLabelValues.Length);
            Array.Copy(labelValues, 0, labels, this.defaultLabelValues.Length, labelValues.Length);
            return labels;
        }
    }
}
