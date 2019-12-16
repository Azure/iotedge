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

        public MetricFilter(Dictionary<string, string> tagsWhitelist = null, Dictionary<string, string> tagsBlacklist = null, Dictionary<string, string> tagsToAdd = null, List<string> tagsToRemove = null)
        {
            this.tagsWhitelist = tagsWhitelist;
            this.tagsBlacklist = tagsBlacklist;
            this.tagsToAdd = tagsToAdd;
            this.tagsToRemove = tagsToRemove;
        }

        public IEnumerable<Metric> FilterMetrics(IEnumerable<Metric> metrics)
        {
            foreach (Metric metric in metrics)
            {
                Dictionary<string, string> tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(metric.Tags);

                // Skip if the blacklist has any or the whitelist does not contain it.
                if ((this.tagsBlacklist != null && this.tagsBlacklist.Any(tags.Contains)) || (this.tagsWhitelist != null && !this.tagsWhitelist.Any(tags.Contains)))
                {
                    continue;
                }

                // Add or remove tags if needed.
                if ((this.tagsToAdd != null && this.tagsToAdd.Any()) || (this.tagsToRemove != null && this.tagsToRemove.Any()))
                {
                    this.tagsToAdd?.ToList().ForEach(toAdd => tags.Add(toAdd.Key, toAdd.Value));
                    this.tagsToRemove?.ForEach(toRemove => tags.Remove(toRemove));

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
