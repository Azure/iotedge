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
    public class MetricTransformer
    {
        Option<List<KeyValuePair<string, string>>> allowedTags = Option.None<List<KeyValuePair<string, string>>>();
        Option<List<KeyValuePair<string, string>>> tagsToAdd = Option.None<List<KeyValuePair<string, string>>>();
        Option<List<string>> tagsToRemove = Option.None<List<string>>();
        Option<List<string>> tagsToHash = Option.None<List<string>>();

        public IEnumerable<Metric> TransformMetrics(IEnumerable<Metric> metrics)
        {
            foreach (Metric metric in metrics)
            {
                // Skip metric if it doesn't contain any allowed tags.
                if (this.allowedTags.Exists(wl => !wl.Any(metric.Tags.Contains)))
                {
                    continue;
                }

                // Modify tags if needed.
                if (this.tagsToAdd.HasValue || this.tagsToRemove.HasValue)
                {
                    Dictionary<string, string> newTags = metric.Tags.ToDictionary(t => t.Key, t => t.Value);

                    this.tagsToAdd.ForEach(tta => tta.ForEach(toAdd => newTags.Add(toAdd.Key, toAdd.Value)));
                    this.tagsToRemove.ForEach(ttr => ttr.ForEach(toRemove => newTags.Remove(toRemove)));
                    this.tagsToHash.ForEach(tth => tth.ForEach(toHash =>
                    {
                        if (newTags.TryGetValue(toHash, out string value))
                        {
                            newTags[toHash] = value.CreateSha256();
                        }
                    }));

                    yield return new Metric(metric.TimeGeneratedUtc, metric.Name, metric.Value, newTags);
                }
                else
                {
                    yield return metric;
                }
            }
        }

        public MetricTransformer AddAllowedTags(params KeyValuePair<string, string>[] pairs)
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

        public MetricTransformer AddTagsToAdd(params KeyValuePair<string, string>[] pairs)
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

        public MetricTransformer AddTagsToRemove(params string[] keys)
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

        public MetricTransformer AddTagsToHash(params string[] keys)
        {
            if (this.tagsToHash.HasValue)
            {
                this.tagsToHash.ForEach(wl => wl.AddRange(keys));
            }
            else
            {
                this.tagsToHash = Option.Some(new List<string>(keys));
            }

            return this;
        }
    }
}
