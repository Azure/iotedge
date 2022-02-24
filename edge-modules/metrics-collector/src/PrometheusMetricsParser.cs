// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class PrometheusMetricsParser
    {
        // Extracts data from prometheus format. Read about prometheus format here:
        //
        // - https://prometheus.io/docs/concepts/data_model/
        // - https://prometheus.io/docs/instrumenting/exposition_formats/
        // - https://github.com/prometheus/common/blob/main/expfmt/text_parse.go
        //
        // Example:
        //
        //     edgeagent_total_disk_read_bytes{iothub="lefitche-hub-3.azure-devices.net",edge_device="device4",instance_number="1",module="edgeHub"} 32908854
        private const string MetricName = @"(?<metricname>[a-zA-Z_:][a-zA-Z_0-9:]*)";
        private const string MetricValue = @"(?<metricvalue>[\d\.eE+-]+|NaN)";
        private const string MetricTimestamp = @"(?<metrictimestamp>[\d]+)";
        private const string LabelName = @"(?<labelname>[a-zA-Z_][a-zA-Z_0-9]*)";
        private const string LabelValue = @"(?<labelvalue>(?:[^\\""]|\\""|\\\\|\\n)*)";
        private const string Whitespace = @"[ \t]";
        private static readonly string Label = $"{LabelName}{Whitespace}*={Whitespace}*\"{LabelValue}\"{Whitespace}*";
        private static readonly Regex PrometheusSchemaRegex = new Regex(
            @"^" +
                MetricName +
                $"{Whitespace}*" +
                @"(?:" +
                    @"\{" +
                    $"{Whitespace}*" +
                    @"(?:" +
                        Label +
                        @"(?:" +
                            @"," +
                            $"{Whitespace}*" +
                            Label +
                        @")*" +
                        @"(?:" +
                            @"," +
                            $"{Whitespace}*" +
                        @")?" +
                    @")?" +
                    @"\}" +
                    $"{Whitespace}*" +
                @")?" +
                MetricValue +
                @"(?:" +
                    $"{Whitespace}+" +
                    MetricTimestamp +
                @")?" +
            @"$",
            RegexOptions.Compiled);

        private static readonly DateTime MetricTimestampEpoch = DateTime.UnixEpoch;

        public static IEnumerable<Metric> ParseMetrics(DateTime timeGeneratedUtc, string prometheusMessage, string endpoint=null)
        {
            Preconditions.CheckArgument(timeGeneratedUtc.Kind == DateTimeKind.Utc, $"Metric {nameof(timeGeneratedUtc)} parameter only supports in UTC.");
            using (StringReader sr = new StringReader(prometheusMessage))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#')
                    {
                        // Empty line or comment.
                        continue;
                    }

                    Match match = PrometheusSchemaRegex.Match(line);
                    if (!match.Success)
                    {
                        LoggerUtil.Writer.LogWarning($"Ignoring metric line because it does not match the expected format: [{line}]");
                        continue;
                    }

                    double metricValue;
                    if (!double.TryParse(match.Groups["metricvalue"]?.Value, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out metricValue))
                    {
                        LoggerUtil.Writer.LogWarning($"Ignoring metric line because the metric value is malformed: [{line}]");
                        continue;
                    }

                    string metricName = match.Groups["metricname"]?.Value ?? string.Empty;
                    var tagNames = (match.Groups["labelname"].Captures as IEnumerable<Capture>).Select(c => c.Value);
                    var tagValues =
                        (match.Groups["labelvalue"].Captures as IEnumerable<Capture>)
                        .Select(value =>
                        {
                            // Evaluate escaped characters
                            string result = string.Empty;
                            bool escaped = false;
                            foreach (char c in value.Value)
                            {
                                if (escaped)
                                {
                                    if (c == 'n')
                                    {
                                        result += "\n";
                                    }
                                    else
                                    {
                                        result += c;
                                    }

                                    escaped = false;
                                }
                                else if (c == '\\')
                                {
                                    escaped = true;
                                }
                                else
                                {
                                    result += c;
                                }
                            }

                            return result;
                        });

                    Dictionary<string, string> tags = tagNames.Zip(tagValues, (k, v) => new { k, v })
                        .ToDictionary(x => x.k, x => x.v);

                    string metricTimestampString = match.Groups["metrictimestamp"]?.Value;
                    if (metricTimestampString.Length != 0)
                    {
                        long metricTimestamp;
                        if (!long.TryParse(metricTimestampString, out metricTimestamp))
                        {
                            LoggerUtil.Writer.LogWarning($"Ignoring metric line because the metric timestamp is malformed: [{line}]");
                            continue;
                        }

                        timeGeneratedUtc = MetricTimestampEpoch + TimeSpan.FromMilliseconds(metricTimestamp);
                    }

                    if (Settings.Current.AddIdentifyingTags)
                    {
                        if (!tags.ContainsKey("edge_device")) tags["edge_device"] = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
                        if (!tags.ContainsKey("iothub")) tags["iothub"] = Environment.GetEnvironmentVariable("IOTEDGE_IOTHUBHOSTNAME");
                        if (!tags.ContainsKey("module_name") && endpoint != null) tags["module_name"] = new Uri(endpoint).Host;
                    }

                    yield return new Metric(
                        timeGeneratedUtc,
                        metricName,
                        metricValue,
                        tags,
                        endpoint);
                }
            }
        }
    }
}
