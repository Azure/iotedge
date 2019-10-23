// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;

    public class MetricsParser
    {
        const string IdentifierPropertyName = "MessageIdentifier";
        const string PrometheusMetricSchema = @"^(?<metricname>[^#\{\}]+)(\{((?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\"")(,(?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\""))*)\})?\s(?<metricvalue>.+)$";
        static readonly Regex PrometheusSchemaRegex = new Regex(PrometheusMetricSchema, RegexOptions.Compiled);

        public IList<Metric> ParseMetrics(DateTime timeGeneratedUtc, string prometheusMessage)
        {
            var metricsDataList = new List<Metric>();
            using (StringReader sr = new StringReader(prometheusMessage))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Trim().StartsWith("#"))
                    {
                        continue;
                    }

                    Match match = PrometheusSchemaRegex.Match(line.Trim());
                    if (match.Success)
                    {
                        var metricName = string.Empty;
                        var metricValue = string.Empty;
                        var tagNames = new List<string>();
                        var tagValues = new List<string>();

                        var name = match.Groups["metricname"];
                        if (name?.Length > 0)
                        {
                            metricName = name.Value;
                        }

                        var value = match.Groups["metricvalue"];
                        if (value?.Length > 0)
                        {
                            metricValue = value.Value;
                        }

                        var tagnames = match.Groups["tagname"];
                        if (tagnames.Length > 0)
                        {
                            for (int i = 0; i < tagnames.Captures.Count; i++)
                            {
                                tagNames.Add(tagnames.Captures[i].Value);
                            }
                        }

                        var tagvalues = match.Groups["tagvalue"];
                        if (tagvalues.Length > 0)
                        {
                            for (int i = 0; i < tagvalues.Captures.Count; i++)
                            {
                                tagValues.Add(tagvalues.Captures[i].Value);
                            }
                        }

                        var tags = tagNames.Zip(tagValues, (k, v) => new { k, v })
                            .ToDictionary(x => x.k, x => x.v);
                        var metricsData = new Metric(
                            timeGeneratedUtc,
                            "prometheus",
                            metricName,
                            metricValue,
                            JsonConvert.SerializeObject(tags));

                        metricsDataList.Add(metricsData);
                    }
                }
            }

            return metricsDataList;
        }
    }

    public class Metric
    {
        public DateTime TimeGeneratedUtc { get; set; }
        public string Namespace { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Tags { get; set; }

        public Metric(DateTime timeGeneratedUtc, string @namespace, string name, string value, string tags)
        {
            this.TimeGeneratedUtc = timeGeneratedUtc;
            this.Namespace = @namespace;
            this.Name = name;
            this.Value = value;
            this.Tags = tags;
        }

        public int GetValuelessHash()
        {
            // TODO: replace with
            // return HashCode.Combine(Namespace.GetHashCode(), Name.GetHashCode(), Tags.GetHashCode());
            int hash = 17;
            hash = hash * 31 + this.Namespace.GetHashCode();
            hash = hash * 31 + this.Name.GetHashCode();
            hash = hash * 31 + this.Tags.GetHashCode();

            return hash;
        }
    }
}
