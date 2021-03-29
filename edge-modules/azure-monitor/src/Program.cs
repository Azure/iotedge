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

    using System.Diagnostics;

    internal static class Program
    {
        private static void WaitForDebugger()
        {
            Logger.Writer.LogInformation("Waiting for debugger to attach");
            for(int i = 0; i < 300 && !Debugger.IsAttached; i++)
            {
                Thread.Sleep(100);
            }
            Thread.Sleep(250);
        }

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger.Writer);

            // wait up to 30 seconds for debugger to attach if in a debug build
            #if DEBUG
            WaitForDebugger();
            #endif

            Logger.Writer.LogInformation($"Starting metrics collector with the following settings:\r\n{Settings.Current}");

            // start periodic telemetry reporting
            ResourceUsageTelemetryReporter telemReporter = new ResourceUsageTelemetryReporter(Logger.Writer);

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] transportSettings = { mqttSetting };
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
                Logger.Writer.LogError(e, "Error occurred during metrics collection setup.");
            }
            finally
            {
                moduleClient?.Dispose();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));

            Logger.Writer.LogInformation("MetricsCollector Main() finished.");
            return 0;
        }

        //TODO: we don't use the module twin for anything, don't bother getting tags?
        static async Task<Option<SortedDictionary<string, string>>> GetAdditionalTagsFromTwin(ModuleClient moduleClient)
        {
            Twin twin = await moduleClient.GetTwinAsync();
            TwinCollection desiredProperties = twin.Properties.Desired;
            Logger.Writer.LogInformation($"Received {desiredProperties.Count} tags from module twin's desired properties that will be added to scraped metrics");

            const string additionalTagsPlaceholder = "additionalTags";
            Dictionary<string, string> deserializedTwin = JsonConvert.DeserializeObject<Dictionary<string, string>>(twin.Properties.Desired.ToJson());
            if (deserializedTwin.ContainsKey(additionalTagsPlaceholder))
            {
                return Option.Some<SortedDictionary<string, string>>(ParseKeyValuePairs(deserializedTwin[additionalTagsPlaceholder], Logger.Writer, true));
            }

            return Option.None<SortedDictionary<string, string>>();
        }

        // Test info is used in the longhaul, stress, and connectivity tests to provide contextual information when reporting.
        // This info is passed from the vsts pipeline and needs to be parsed by the test modules.
        // Includes information such as build numbers, ids, host platform, etc.
        // Argument should be in the format key=value[,key=value]
        public static SortedDictionary<string, string> ParseKeyValuePairs(string keyValuePairs, ILogger logger, bool shouldBeNonEmpty)
        {
            Logger.Writer.LogInformation($"Parsing key value pairs: {keyValuePairs}");

            Dictionary<string, string> unsortedParsedTestInfo = keyValuePairs.Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => (KeyAndValue: x, SplitIndex: x.IndexOf('=')))
                                .Where(x => x.SplitIndex >= 1)
                                .ToDictionary(
                                    x => x.KeyAndValue.Substring(0, x.SplitIndex),
                                    x => x.KeyAndValue.Substring(x.SplitIndex + 1, x.KeyAndValue.Length - x.SplitIndex - 1));

            if (shouldBeNonEmpty)
            {
                Preconditions.CheckArgument(unsortedParsedTestInfo.Count > 0, $"Key value pairs not in correct format: {keyValuePairs}");
            }

            return new SortedDictionary<string, string>(unsortedParsedTestInfo);
        }
    }
}
