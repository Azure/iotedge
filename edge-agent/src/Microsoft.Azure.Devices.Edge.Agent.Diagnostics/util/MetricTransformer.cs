// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util
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
        Option<List<KeyValuePair<string, string>>> disallowedTags = Option.None<List<KeyValuePair<string, string>>>();
        Option<List<KeyValuePair<string, string>>> tagsToAdd = Option.None<List<KeyValuePair<string, string>>>();
        Option<List<(string tag, Func<string, string> valueTransformer)>> tagsToModify = Option.None<List<(string, Func<string, string>)>>();
        Option<List<string>> tagsToRemove = Option.None<List<string>>();

        public IEnumerable<Metric> TransformMetrics(IEnumerable<Metric> metrics)
        {
            foreach (Metric metric in metrics)
            {
                // Skip metric if it doesn't contain any allowed tags.
                if (this.allowedTags.Exists(allowedTags => !allowedTags.Any(metric.Tags.Contains)))
                {
                    continue;
                }

                // Skip metric if it contains any disallowed tags.
                if (this.disallowedTags.Exists(disallowedTags => disallowedTags.Any(metric.Tags.Contains)))
                {
                    continue;
                }

                // Modify tags if needed.
                if (this.tagsToAdd.HasValue || this.tagsToRemove.HasValue || this.tagsToModify.HasValue)
                {
                    Dictionary<string, string> newTags = metric.Tags.ToDictionary(t => t.Key, t => t.Value);

                    this.tagsToAdd.ForEach(tagsToAdd => tagsToAdd.ForEach(toAdd => newTags.Add(toAdd.Key, toAdd.Value)));
                    this.tagsToRemove.ForEach(tagsToRemove => tagsToRemove.ForEach(toRemove => newTags.Remove(toRemove)));

                    this.tagsToModify.ForEach(tagsToModify => tagsToModify.ForEach(modification =>
                    {
                        if (newTags.TryGetValue(modification.tag, out string oldValue))
                        {
                            newTags[modification.tag] = modification.valueTransformer(oldValue);
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

        public MetricTransformer AddAllowedTags(params (string key, string value)[] pairs)
        {
            if (this.allowedTags.HasValue)
            {
                this.allowedTags.ForEach(at => at.AddRange(pairs.Select(p => new KeyValuePair<string, string>(p.key, p.value))));
            }
            else
            {
                this.allowedTags = Option.Some(pairs.Select(p => new KeyValuePair<string, string>(p.key, p.value)).ToList());
            }

            return this;
        }

        public MetricTransformer AddDisallowedTags(params (string key, string value)[] pairs)
        {
            if (this.disallowedTags.HasValue)
            {
                this.disallowedTags.ForEach(dt => dt.AddRange(pairs.Select(p => new KeyValuePair<string, string>(p.key, p.value))));
            }
            else
            {
                this.disallowedTags = Option.Some(pairs.Select(p => new KeyValuePair<string, string>(p.key, p.value)).ToList());
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

        /// <summary>
        /// This replaces the value of the given tag with the result of the valueTransformer function.
        /// </summary>
        /// <param name="modifications">Tuple containing tag and transformer function.</param>
        /// <returns>self.</returns>
        public MetricTransformer AddTagsToModify(params (string tag, Func<string, string> valueTransformer)[] modifications)
        {
            if (this.tagsToModify.HasValue)
            {
                this.tagsToModify.ForEach(wl => wl.AddRange(modifications));
            }
            else
            {
                this.tagsToModify = Option.Some(new List<(string, Func<string, string>)>(modifications));
            }

            return this;
        }
    }
}
