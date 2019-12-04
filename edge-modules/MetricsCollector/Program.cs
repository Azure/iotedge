// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");

        public static int Main() => MainAsync().Result;

        private static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting metrics collector with the following settings:\r\n{Settings.Current}");

            MetricsScraper scraper = new MetricsScraper(Settings.Current.Endpoints);
            IMetricsPublisher publisher;
            if (Settings.Current.UploadTarget == UploadTarget.AzureLogAnalytics)
            {
                publisher = new LogAnalyticsUpload(Settings.Current.AzMonWorkspaceId, Settings.Current.AzMonWorkspaceKey, Settings.Current.AzMonLogType, Guid.NewGuid());
            }
            else
            {
                MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                ITransportSettings[] transportSettings = { mqttSetting };
                ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(transportSettings);
                publisher = new EventHubMetricsUpload(moduleClient);
            }

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);
            using (MetricsScrapeAndUpload metricsScrapeAndUpload = new MetricsScrapeAndUpload(scraper, publisher, Guid.NewGuid()))
            {
                TimeSpan scrapeAndUploadInterval = TimeSpan.FromSeconds(Settings.Current.ScrapeFrequencySecs);
                metricsScrapeAndUpload.Start(scrapeAndUploadInterval);
                await cts.Token.WhenCanceled();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));

            Logger.LogInformation("MetricsCollector Main() finished.");
            return 0;
        }
    }
}
