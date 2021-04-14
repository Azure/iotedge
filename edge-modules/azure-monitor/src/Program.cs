// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;


    using System.Diagnostics;

    internal static class Program
    {
        private static void WaitForDebugger()
        {
            LoggerUtil.Writer.LogInformation("Waiting for debugger to attach");
            for(int i = 0; i < 300 && !Debugger.IsAttached; i++)
            {
                Thread.Sleep(100);
            }
            Thread.Sleep(250);
        }

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), LoggerUtil.Writer);

            // wait up to 30 seconds for debugger to attach if in a debug build
            #if DEBUG
            WaitForDebugger();
#endif

            LoggerUtil.Writer.LogInformation($"Starting metrics collector with the following settings:\r\n{Settings.Current}");
            var transportSetting = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
            
            ITransportSettings[] transportSettings = { transportSetting };
            ModuleClient moduleClient = null;
            try
            {
                moduleClient = await ModuleClient.CreateFromEnvironmentAsync(transportSettings);
                moduleClient.ProductInfo = Constants.ProductInfo;
                Option<SortedDictionary<string, string>> additionalTags = await GetAdditionalTagsFromTwin(moduleClient);

                MetricsScraper scraper = new MetricsScraper(Settings.Current.Endpoints);
                IMetricsPublisher publisher;
                if (Settings.Current.UploadTarget == UploadTarget.AzureMonitor) {
                    publisher = new FixedSetTableUpload.FixedSetTableUpload(Settings.Current.LogAnalyticsWorkspaceId, Settings.Current.LogAnalyticsWorkspaceKey);
                }
                else {
                    publisher = new IotHubMetricsUpload.IotHubMetricsUpload(moduleClient);
                }

                using (MetricsScrapeAndUpload metricsScrapeAndUpload = new MetricsScrapeAndUpload(scraper, publisher, additionalTags))
                {
                    TimeSpan scrapeAndUploadInterval = TimeSpan.FromSeconds(Settings.Current.ScrapeFrequencySecs);
                    metricsScrapeAndUpload.Start(scrapeAndUploadInterval);
                    // await cts.Token.WhenCanceled();
                    WaitHandle.WaitAny(new WaitHandle[] {cts.Token.WaitHandle });
                }
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError(e, "Error occurred during metrics collection setup.");
            }
            finally
            {
                moduleClient?.Dispose();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));

            LoggerUtil.Writer.LogInformation("MetricsCollector Main() finished.");
            return 0;
        }

        //TODO: we don't use the module twin for anything, don't bother getting tags?
        static async Task<Option<SortedDictionary<string, string>>> GetAdditionalTagsFromTwin(ModuleClient moduleClient)
        {
            Twin twin = await moduleClient.GetTwinAsync();
            TwinCollection desiredProperties = twin.Properties.Desired;
            LoggerUtil.Writer.LogInformation($"Received {desiredProperties.Count} tags from module twin's desired properties that will be added to scraped metrics");

            const string additionalTagsPlaceholder = "additionalTags";
            Dictionary<string, string> deserializedTwin = JsonConvert.DeserializeObject<Dictionary<string, string>>(twin.Properties.Desired.ToJson());
            if (deserializedTwin.ContainsKey(additionalTagsPlaceholder))
            {
                return Option.Some<SortedDictionary<string, string>>(ModuleUtil.ParseKeyValuePairs(deserializedTwin[additionalTagsPlaceholder], LoggerUtil.Writer, true));
            }

            return Option.None<SortedDictionary<string, string>>();
        }
    }
}
