// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.IotHubMetricsUpload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.Util;
    using ModuleClientWrapper;

    public class IotHubMetricsUpload : IMetricsPublisher
    {
        const string IdentifierPropertyName = "id";
        readonly IModuleClientWrapper ModuleClientWrapper;

        public IotHubMetricsUpload(IModuleClientWrapper moduleClientWrapper)
        {
            this.ModuleClientWrapper = Preconditions.CheckNotNull(moduleClientWrapper, nameof(moduleClientWrapper));
        }

        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            try
            {
                Preconditions.CheckNotNull(metrics, nameof(metrics));
                IEnumerable<ExportMetric> outputMetrics = metrics.Select(m => new ExportMetric(m));

                string outputString = JsonConvert.SerializeObject(outputMetrics);

                if (Settings.Current.TransformForIoTCentral)
                {
                    outputString = Transform(outputMetrics);
                }

                byte[] metricsData = Encoding.UTF8.GetBytes(outputString);
                if (Settings.Current.CompressForUpload)
                {
                    metricsData = Compression.CompressToGzip(metricsData);
                }

                Message metricsMessage = new Message(metricsData);
                metricsMessage.Properties[IdentifierPropertyName] = Constants.IoTUploadMessageIdentifier;

                await this.ModuleClientWrapper.SendMessageAsync("metricOutput", metricsMessage);

                return true;
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError(e, "Error sending metrics via IoT message");
                return false;
            }
        }

        private string Transform(IEnumerable<ExportMetric> metrics)
        {
            DateTime timeGeneratedUtc = DateTime.MaxValue;
            string deviceName = string.Empty;
            string instanceNumber = string.Empty;
            Dictionary<string, List<Dictionary<string, object>>> categorizedMetrics = new Dictionary<string, List<Dictionary<string, object>>>();
            foreach (ExportMetric metric in metrics)
            {
                if (timeGeneratedUtc == DateTime.MaxValue)
                {
                    timeGeneratedUtc = metric.TimeGeneratedUtc;
                }

                if (deviceName == string.Empty)
                {
                    deviceName = metric.Labels.GetValueOrDefault("edge_device", string.Empty);
                }

                if (instanceNumber == string.Empty)
                {
                    instanceNumber = metric.Labels.GetValueOrDefault("instance_number", string.Empty);
                }

                if (double.IsNaN(metric.Value) ||
                    double.IsNegativeInfinity(metric.Value) ||
                    double.IsPositiveInfinity(metric.Value))
                {
                    continue;
                }

                var labels = TransformMetricLabels(metric);
                if (categorizedMetrics.ContainsKey(metric.Name))
                {
                    List<Dictionary<string, object>> list;
                    if (categorizedMetrics.TryGetValue(metric.Name, out list))
                    {
                        list.Add(labels);
                    }
                }
                else
                {
                    List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
                    list.Add(labels);
                    categorizedMetrics.Add(metric.Name, list);
                }
            }

            string outputMessageString = "{" + $"\"TimeGeneratedUtc\":\"{timeGeneratedUtc}\", \"edge_device\":\"{deviceName}\", \"instance_number\":\"{instanceNumber}\"";
            foreach (KeyValuePair<string, List<Dictionary<string, object>>> kvp in categorizedMetrics)
            {
                foreach (var obj in kvp.Value)
                {
                    outputMessageString += $",\"{kvp.Key}\":" + JsonConvert.SerializeObject(obj);
                }
            }

            outputMessageString += "}";
            return outputMessageString;
        }

        private Dictionary<string, object> TransformMetricLabels(ExportMetric metric)
        {
            var labels = new Dictionary<string, object>();
            labels.Add("value", metric.Value);

            string command = metric.Labels.GetValueOrDefault("command", string.Empty);
            if (command != string.Empty)
            {
                labels.Add("command", command);
            }

            string diskName = metric.Labels.GetValueOrDefault("disk_name", string.Empty);
            if (diskName != string.Empty)
            {
                labels.Add("disk_name", diskName);
            }

            string from = metric.Labels.GetValueOrDefault("from", string.Empty);
            if (from == string.Empty)
            {
                from = metric.Labels.GetValueOrDefault("module_name", string.Empty);
                if (from == string.Empty)
                {
                    from = metric.Labels.GetValueOrDefault("id", string.Empty);
                    if (from != string.Empty && from.Contains("[IotHubHostName:"))
                    {
                        // Process ids like: 'DeviceId: <deviceId>; ModuleId: <moduleName> [IotHubHostName: <iothubFullyQualifiedName>]'
                        // to make them look like <deviceId>/<moduleId>
                        string device = string.Empty;
                        string module = string.Empty;
                        string temp = from.Replace('[', ';');
                        string[] idParts = temp.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (string idPart in idParts)
                        {
                            string part = idPart.Trim().ToLowerInvariant();
                            string[] subParts = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
                            if (subParts.Length == 2)
                            {
                                if (subParts[0].Trim() == "deviceid")
                                {
                                    device = subParts[1].Trim();
                                    continue;
                                }

                                if (subParts[0].Trim() == "moduleid")
                                {
                                    module = subParts[1].Trim();
                                    continue;
                                }
                            }
                        }

                        if (device == string.Empty || module == string.Empty)
                        {
                            LoggerUtil.Writer.LogInformation($"skipped transforming from: '{from}' in metric: '{metric.Name}'");
                            from = string.Empty;
                        }
                        else
                        {
                            from = $"{device}/{module}";
                        }
                    }
                }
            }
            if (from != string.Empty)
            {
                labels.Add("from", from);
            }

            string fromRouteOutput = metric.Labels.GetValueOrDefault("from_route_output", string.Empty);
            if (fromRouteOutput == string.Empty)
            {
                fromRouteOutput = metric.Labels.GetValueOrDefault("route_output", string.Empty);
                if (fromRouteOutput == string.Empty)
                {
                    fromRouteOutput = metric.Labels.GetValueOrDefault("source", string.Empty);
                }
            }
            if (fromRouteOutput != string.Empty)
            {
                labels.Add("from_route_output", fromRouteOutput);
            }

            string to = metric.Labels.GetValueOrDefault("to", string.Empty);
            if (to == string.Empty)
            {
                to = metric.Labels.GetValueOrDefault("to_route_input", string.Empty);
                if (to == string.Empty)
                {
                    to = metric.Labels.GetValueOrDefault("target", string.Empty);
                }
            }
            if (to != string.Empty)
            {
                labels.Add("to", to);
            }

            string priorityString = metric.Labels.GetValueOrDefault("priority", string.Empty);
            if (priorityString != string.Empty)
            {
                try
                {
                    long priority = Convert.ToInt64(priorityString);
                    labels.Add("priority", priority);
                }
                catch
                {
                    LoggerUtil.Writer.LogInformation($"skipped transforming priority: '{priorityString}' in metric: '{metric.Name}'");
                }
            }

            string quantileString = metric.Labels.GetValueOrDefault("quantile", string.Empty);
            if (quantileString != string.Empty)
            {
                double quantile;
                if (double.TryParse(quantileString, out quantile) &&
                    !(double.IsNaN(quantile) ||
                      double.IsNegativeInfinity(quantile) ||
                      double.IsNegativeInfinity(quantile)))
                {
                    labels.Add("quantile", quantile);
                }
                else
                {
                    LoggerUtil.Writer.LogInformation($"skipped transforming quantile: '{quantileString}' in metric: '{metric.Name}'");
                }
            }

            return labels;
        }

        class ExportMetric
        {
            public DateTime TimeGeneratedUtc { get; }
            public string Name { get; }
            public double Value { get; }
            public IReadOnlyDictionary<string, string> Labels { get; }

            public ExportMetric(Metric baseMetric)
            {
                Preconditions.CheckArgument(baseMetric.TimeGeneratedUtc.Kind == DateTimeKind.Utc, $"Metric {nameof(baseMetric.TimeGeneratedUtc)} parameter only supports in UTC.");
                this.TimeGeneratedUtc = baseMetric.TimeGeneratedUtc;
                this.Name = Preconditions.CheckNotNull(baseMetric.Name, nameof(baseMetric.Name));
                this.Value = baseMetric.Value;
                this.Labels = Preconditions.CheckNotNull(baseMetric.Tags, nameof(baseMetric.Tags));
            }
        }
    }
}
