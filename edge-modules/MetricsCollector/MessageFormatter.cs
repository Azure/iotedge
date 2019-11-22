// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class MessageFormatter
    {
        const string IdentifierPropertyName = "MessageIdentifier";
        const string PrometheusMetricSchema = @"^(?<metricname>[^#\{\}]+)(\{((?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\"")(,(?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\""))*)\})?\s(?<metricvalue>.+)$";
        static readonly Regex PrometheusSchemaRegex = new Regex(PrometheusMetricSchema, RegexOptions.Compiled);
        readonly MetricsFormat format;
        readonly string identifier;

        public MessageFormatter(MetricsFormat format, string identifier)
        {
            this.format = format;
            this.identifier = identifier;
        }

        // For Log Analytics upload
        public string BuildJSON(string prometheusMessage)
        {
            return JsonConvert.SerializeObject(this.GetMetricsDataList(prometheusMessage));
        }

        // For IoT Hub upload
        public IList<Message> Build(string prometheusMessage)
        {
            IList<Message> messages = this.format == MetricsFormat.Prometheus
                ? this.BuildMessagesInPrometheusFormat(prometheusMessage)
                : this.BuildMessagesInJsonFormat(prometheusMessage);
            return messages;
        }

        IList<Message> BuildMessagesInJsonFormat(string prometheusMessage)
        {
            IList<MetricsData> metricsDataList = this.GetMetricsDataList(prometheusMessage);
            byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metricsDataList));
            Message message = new Message(messageBytes);
            message.Properties[IdentifierPropertyName] = this.identifier;
            message.MessageSchema = this.format.ToString();
            message.ContentEncoding = "UTF-8";
            message.ContentType = "application/json";

            return new List<Message> { message };
        }

        IList<MetricsData> GetMetricsDataList(string prometheusMessage)
        {
            List<MetricsData> metricsDataList = new List<MetricsData>();
            using (StringReader sr = new StringReader(prometheusMessage))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Trim().StartsWith('#'))
                    {
                        continue;
                    }

                    Match match = PrometheusSchemaRegex.Match(line.Trim());
                    if (match.Success)
                    {
                        string metricName = string.Empty;
                        string metricValue = string.Empty;
                        List<string> tagNames = new List<string>();
                        List<string> tagValues = new List<string>();

                        Group name = match.Groups["metricname"];
                        if (name?.Length > 0)
                        {
                            metricName = name.Value;
                        }

                        Group value = match.Groups["metricvalue"];
                        if (value?.Length > 0)
                        {
                            metricValue = value.Value;
                        }

                        Group tagnames = match.Groups["tagname"];
                        if (tagnames.Length > 0)
                        {
                            for (int i = 0; i < tagnames.Captures.Count; i++)
                            {
                                tagNames.Add(tagnames.Captures[i].Value);
                            }
                        }

                        Group tagvalues = match.Groups["tagvalue"];
                        if (tagvalues.Length > 0)
                        {
                            for (int i = 0; i < tagvalues.Captures.Count; i++)
                            {
                                tagValues.Add(tagvalues.Captures[i].Value);
                            }
                        }

                        Dictionary<string, string> tags = tagNames.Zip(tagValues, (k, v) => new { k, v })
                            .ToDictionary(x => x.k, x => x.v);
                        MetricsData metricsData = new MetricsData(
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

        IList<Message> BuildMessagesInPrometheusFormat(string prometheusMessage)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(prometheusMessage);
            Message message = new Message(messageBytes);
            message.Properties[IdentifierPropertyName] = this.identifier;
            message.MessageSchema = this.format.ToString();
            return new[] { message };
        }
    }

    public class MetricsData
    {
        public MetricsData(string ns, string name, string value, string tags)
        {
            this.TimeGeneratedUtc = DateTime.UtcNow;
            this.Namespace = Preconditions.CheckNotNull(ns);
            this.Name = Preconditions.CheckNotNull(name);
            this.Value = Preconditions.CheckNotNull(value);
            this.Tags = Preconditions.CheckNotNull(tags);
        }

        public DateTime TimeGeneratedUtc { get; }
        public string Namespace { get; }
        public string Name { get; }
        public string Value { get; }
        public string Tags { get; }
    }
}
