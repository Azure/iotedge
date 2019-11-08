// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;

    public static class PrometheousMetricsParser
    {
        // Extracts data from prometheous format. Read about prometheous format here:
        // https://prometheus.io/docs/concepts/data_model/
        const string PrometheusMetricSchema = @"^(?<metricname>[^#\{\}]+)(\{((?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\"")(,(?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\""))*)\})?\s(?<metricvalue>.+)$";
        static readonly Regex PrometheusSchemaRegex = new Regex(PrometheusMetricSchema, RegexOptions.Compiled);

        public static IEnumerable<Metric> ParseMetrics(DateTime timeGeneratedUtc, string prometheusMessage)
        {
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

                        var valueGroup = match.Groups["metricvalue"];
                        if (valueGroup?.Length > 0)
                        {
                            metricValue = valueGroup.Value;
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

                        if (double.TryParse(metricValue, out double value))
                        {
                            yield return new Metric(
                                timeGeneratedUtc,
                                metricName,
                                value,
                                JsonConvert.SerializeObject(tags));
                        }
                    }
                }
            }
        }
    }
}
