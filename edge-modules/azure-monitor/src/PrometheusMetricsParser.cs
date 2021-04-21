// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class PrometheusMetricsParser
    {
        // Extracts data from prometheous format. Read about prometheous format here:
        // https://prometheus.io/docs/concepts/data_model/
        // Example:
        // edgeagent_total_disk_read_bytes{iothub="lefitche-hub-3.azure-devices.net",edge_device="device4",instance_number="1",module="edgeHub"} 32908854
        private static readonly Regex PrometheusSchemaRegex = new Regex(
            @"^(?<metricname>[^#\{\}]+)" +  // metric name. The prometheus spec says metric names should match the regex [a-zA-Z_:][a-zA-Z0-9_:]*, but it doesn't hurt to be more permissive here
            @"(\{(?:(?:(?<tagname>[^="",]+)=(?:\""(?<tagvalue>(?:\\.|[^\\""])*)\"")" +  // first tag (name="value" pair)
            @"(?:,(?<tagname>[^="",]+)=(?:\""(?<tagvalue>(?:\\.|[^\\""])*)\""))*))?\})?\s" +  // all following tags
            @"(?<metricvalue>[\d\.eE+-]+|NaN)$"  // metric value (digits and .eE+-)
            , RegexOptions.Compiled);

        public static IEnumerable<Metric> ParseMetrics(DateTime timeGeneratedUtc, string prometheusMessage, string endpoint=null)
        {
            Preconditions.CheckArgument(timeGeneratedUtc.Kind == DateTimeKind.Utc, $"Metric {nameof(timeGeneratedUtc)} parameter only supports in UTC.");
            using (StringReader sr = new StringReader(prometheusMessage))
            {
                string line;
                while ((line = sr.ReadLine()?.Trim()) != null)
                {
                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    Match match = PrometheusSchemaRegex.Match(line);
                    if (match.Success)
                    {
                        double metricValue;
                        if (!double.TryParse(match.Groups["metricvalue"]?.Value, out metricValue))
                        {
                            continue;
                        }

                        string metricName = match.Groups["metricname"]?.Value ?? string.Empty;
                        var tagNames = (match.Groups["tagname"].Captures as IEnumerable<Capture>).Select(c => c.Value);
                        var tagValues = (match.Groups["tagvalue"].Captures as IEnumerable<Capture>).Select(c => c.Value);

                        Dictionary<string, string> tags = tagNames.Zip(tagValues, (k, v) => new { k, v })
                            .ToDictionary(x => x.k, x => x.v);

                        yield return new Metric(
                            timeGeneratedUtc,
                            metricName,
                            metricValue,
                            tags,
                            endpoint: endpoint);
                    }
                    else {
                        LoggerUtil.Writer.LogInformation("Dropped malformed metric \"" + line + "\", (only numerical metric values are supported)");
                    }
                }
            }
        }
    }
}
