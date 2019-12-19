// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class EventHubMetricsUpload : IMetricsPublisher
    {
        readonly ModuleClient moduleClient;
        static readonly ILogger Log = Logger.Factory.CreateLogger<LogAnalyticsUpload>();

        public EventHubMetricsUpload(ModuleClient moduleClient)
        {
            this.moduleClient = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
        }

        public async Task<bool> PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            try
            {
                Preconditions.CheckNotNull(metrics, nameof(metrics));
                byte[] metricsData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metrics));
                Message metricsMessage = new Message(metricsData);

                await this.moduleClient.SendEventAsync(metricsMessage);

                Log.LogInformation("Successfully sent metrics to Event Hub via IoT Hub");
                return true;
            }
            catch (Exception e)
            {
                Log.LogError(e, "Error uploading metrics to Event Hub via IoTHub");
                return false;
            }
        }
    }
}
