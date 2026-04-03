// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    internal class Program
    {
        static readonly ILogger Logger = MetricsUtil.CreateLogger("MetricsCollector");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            Logger.LogInformation($"Starting metrics collector with the following settings:\r\n{Settings.Current}");

            IotHubModuleClient moduleClient = null;
            try
            {
                var mqttSettings = new IotHubClientMqttSettings();
                var clientOptions = new IotHubClientOptions(mqttSettings);
                moduleClient = await IotHubModuleClient.CreateFromEnvironmentAsync(clientOptions);
                Option<SortedDictionary<string, string>> additionalTags = await GetAdditionalTagsFromTwin(moduleClient);

                MetricsScraper scraper = new MetricsScraper(Settings.Current.Endpoints);
                IMetricsPublisher publisher;
                if (Settings.Current.UploadTarget == UploadTarget.AzureLogAnalytics)
                {
                    publisher = new LogAnalyticsUpload(Settings.Current.LogAnalyticsWorkspaceId, Settings.Current.LogAnalyticsWorkspaceKey, Settings.Current.LogAnalyticsLogType);
                }
                else
                {
                    publisher = new IotHubMetricsUpload(moduleClient, Settings.Current.MessageIdentifier);
                }

                using (MetricsScrapeAndUpload metricsScrapeAndUpload = new MetricsScrapeAndUpload(scraper, publisher, additionalTags))
                {
                    TimeSpan scrapeAndUploadInterval = TimeSpan.FromSeconds(Settings.Current.ScrapeFrequencySecs);
                    metricsScrapeAndUpload.Start(scrapeAndUploadInterval);
                    await cts.Token.WhenCanceled();
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error occurred during metrics collection setup.");
            }
            finally
            {
                if (moduleClient != null) await moduleClient.DisposeAsync();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));

            Logger.LogInformation("MetricsCollector Main() finished.");
            return 0;
        }

        static async Task<Option<SortedDictionary<string, string>>> GetAdditionalTagsFromTwin(IotHubModuleClient moduleClient)
        {
            TwinProperties twin = await moduleClient.GetTwinPropertiesAsync();
            PropertyCollection desiredProperties = twin.Desired;
            Logger.LogInformation($"Received {desiredProperties.Count} tags from module twin's desired properties that will be added to scraped metrics");

            string additionalTagsPlaceholder = "additionalTags";
            Dictionary<string, string> deserializedTwin = JsonConvert.DeserializeObject<Dictionary<string, string>>(twin.Desired.GetSerializedString());
            if (deserializedTwin.ContainsKey(additionalTagsPlaceholder))
            {
                return Option.Some<SortedDictionary<string, string>>(ModuleUtil.ParseKeyValuePairs(deserializedTwin[additionalTagsPlaceholder], Logger, true));
            }

            return Option.None<SortedDictionary<string, string>>();
        }
    }
}
