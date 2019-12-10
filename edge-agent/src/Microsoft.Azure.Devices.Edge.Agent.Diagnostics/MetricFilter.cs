// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// This class acts as a pass through for a group of metrics.
    /// It will exclude metrics based on a tag whitelist and blacklist.
    /// After filtering, it will add or remove tags.
    /// </summary>
    public class MetricFilter
    {
        readonly Dictionary<string, string> tagsWhitelist;
        readonly Dictionary<string, string> tagsBlacklist;
        readonly Dictionary<string, string> tagsToAdd;
        readonly List<string> tagsToRemove;

        public MetricFilter(Dictionary<string, string> tagsWhitelist, Dictionary<string, string> tagsBlacklist, Dictionary<string, string> tagsToAdd, List<string> tagsToRemove)
        {
            this.tagsWhitelist = tagsWhitelist ?? throw new ArgumentNullException(nameof(tagsWhitelist));
            this.tagsBlacklist = tagsBlacklist ?? throw new ArgumentNullException(nameof(tagsBlacklist));
            this.tagsToAdd = tagsToAdd ?? throw new ArgumentNullException(nameof(tagsToAdd));
            this.tagsToRemove = tagsToRemove ?? throw new ArgumentNullException(nameof(tagsToRemove));
        }

        public IEnumerable<Metric> FilterMetrics(IEnumerable<Metric> metrics)
        {
            foreach (Metric metric in metrics)
            {
                Dictionary<string, string> tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(metric.Tags);

                // Skip if the blacklist has any or the whitelist does not contain it.
                if (this.tagsBlacklist.Any(tags.Contains) || (this.tagsWhitelist.Any() && !this.tagsWhitelist.Any(tags.Contains)))
                {
                    continue;
                }

                // Add or remove tags if needed.
                if (this.tagsToAdd.Any() || this.tagsToRemove.Any())
                {
                    foreach (var toAdd in this.tagsToAdd)
                    {
                        tags.Add(toAdd.Key, toAdd.Value);
                    }

                    foreach (var toRemove in this.tagsToRemove)
                    {
                        tags.Remove(toRemove);
                    }

                    string newTags = JsonConvert.SerializeObject(tags);
                    yield return new Metric(metric.TimeGeneratedUtc, metric.Name, metric.Value, newTags);
                }
                else
                {
                    yield return metric;
                }
            }
        }
    }
}
