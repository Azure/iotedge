// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    /// <summary>
    /// This class acts as a pass through for a group of metrics.
    /// It will exclude metrics based on a list of allowed tags.
    /// After filtering, it will add or remove tags.
    /// </summary>
    public class MetricFilter
    {
        Option<List<KeyValuePair<string, string>>> allowedTags = Option.None<List<KeyValuePair<string, string>>>();
        Option<List<KeyValuePair<string, string>>> tagsToAdd = Option.None<List<KeyValuePair<string, string>>>();
        Option<List<string>> tagsToRemove = Option.None<List<string>>();

        public IEnumerable<Metric> FilterMetrics(IEnumerable<Metric> metrics)
        {
            foreach (Metric metric in metrics)
            {
                // Skip metric if it doesn't contain any allowed tags.
                if (this.allowedTags.Exists(wl => !wl.Any(metric.Tags.Contains)))
                {
                    continue;
                }

                // Add or remove tags if needed.
                if (this.tagsToAdd.HasValue || this.tagsToRemove.HasValue)
                {
                    Dictionary<string, string> newTags = metric.Tags.ToDictionary(t => t.Key, t => t.Value);

                    this.tagsToAdd.ForEach(tta => tta.ForEach(toAdd => newTags.Add(toAdd.Key, toAdd.Value)));
                    this.tagsToRemove.ForEach(ttr => ttr.ForEach(toRemove => newTags.Remove(toRemove)));

                    yield return new Metric(metric.TimeGeneratedUtc, metric.Name, metric.Value, newTags);
                }
                else
                {
                    yield return metric;
                }
            }
        }

        public MetricFilter AddAllowedTags(params KeyValuePair<string, string>[] pairs)
        {
            if (this.allowedTags.HasValue)
            {
                this.allowedTags.ForEach(wl => wl.AddRange(pairs));
            }
            else
            {
                this.allowedTags = Option.Some(new List<KeyValuePair<string, string>>(pairs));
            }

            return this;
        }

        public MetricFilter AddTagsToAdd(params KeyValuePair<string, string>[] pairs)
        {
            if (this.tagsToAdd.HasValue)
            {
                this.tagsToAdd.ForEach(wl => wl.AddRange(pairs));
            }
            else
            {
                this.tagsToAdd = Option.Some(new List<KeyValuePair<string, string>>(pairs));
            }

            return this;
        }

        public MetricFilter AddTagsToRemove(params string[] keys)
        {
            if (this.tagsToRemove.HasValue)
            {
                this.tagsToRemove.ForEach(wl => wl.AddRange(keys));
            }
            else
            {
                this.tagsToRemove = Option.Some(new List<string>(keys));
            }

            return this;
        }
    }
}
