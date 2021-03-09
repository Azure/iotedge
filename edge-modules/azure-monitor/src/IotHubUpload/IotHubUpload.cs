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

    public class IotHubMetricsUpload : IMetricsPublisher
    {
        const string IdentifierPropertyName = "id";
        readonly ModuleClient moduleClient;

        public IotHubMetricsUpload(ModuleClient moduleClient)
        {
            this.moduleClient = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
        }

        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            try
            {
                Preconditions.CheckNotNull(metrics, nameof(metrics));

                IEnumerable<ExportMetric> outputMetrics = metrics.Select(m => new ExportMetric(m));

                byte[] metricsData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(outputMetrics));
                Message metricsMessage = new Message(metricsData);
                metricsMessage.Properties[IdentifierPropertyName] = Constants.IoTUploadMessageIdentifier;

                await this.moduleClient.SendEventAsync("metricOutput", metricsMessage);

                Logger.Writer.LogInformation("Successfully sent metrics via IoT message");
                return true;
            }
            catch (Exception e)
            {
                Logger.Writer.LogError(e, "Error sending metrics via IoT message");
                return false;
            }
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
