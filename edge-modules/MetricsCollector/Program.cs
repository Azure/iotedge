// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Agent.DiagnosticsComponent;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    internal class Program
    {
        static readonly Version ExpectedSchemaVersion = new Version("1.0");
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        static Timer scrapingTimer;

        public static int Main() => MainAsync().Result;

        private static async Task<int> MainAsync()
        {
            Logger.LogInformation($"Starting metrics collector with the following settings:\r\n{Settings.Current}");

            await InitAsync();

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            // Wait until the app unloads or is cancelled
            AssemblyLoadContext.Default.Unloading += ctx => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            await WhenCancelled(cts.Token, completed, handler).ConfigureAwait(false);
            Logger.LogInformation("MetricsCollector Main() finished.");
            return 0;
        }

        /// <summary>
        ///     Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken, ManualResetEventSlim completed, Option<object> handler)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(
                s =>
                {
                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    ((TaskCompletionSource<bool>)s).SetResult(true);
                },
                tcs);
            return tcs.Task;
        }

        /// <summary>
        ///     Initializes the ModuleClient and sets up the callback to receive
        ///     messages containing temperature information
        /// </summary>
        private static async Task InitAsync()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] transportSettings = { mqttSetting };

            MessageFormatter messageFormatter = new MessageFormatter(Settings.Current.MetricsFormat, Settings.Current.MessageIdentifier);
            Scraper scraper = new Scraper(Settings.Current.Endpoints);

            IMetricsSync metricsSync;
            if (Settings.Current.SyncTarget == SyncTarget.AzureLogAnalytics)
            {
                metricsSync = new LogAnalyticsMetricsSync(messageFormatter, scraper);
            }
            else
            {
                // Open a connection to the Edge runtime
                ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(transportSettings);
                await ioTHubModuleClient.OpenAsync();
                Logger.LogInformation("IoT Hub module client initialized.");
                metricsSync = new IoTHubMetricsSync(messageFormatter, scraper, ioTHubModuleClient);
            }

            TimeSpan scrapingInterval = TimeSpan.FromSeconds(Settings.Current.ScrapeFrequencySecs);
            scrapingTimer = new Timer(ScrapeAndUploadPrometheusMetricsAsync, metricsSync, scrapingInterval, scrapingInterval);

            /*
            add periodic task to scrape and upload
             */
        }

        private static async void ScrapeAndUploadPrometheusMetricsAsync(object context)
        {
            try
            {
                IMetricsSync metricsSync = (IMetricsSync)context;
                await metricsSync.ScrapeAndSyncMetricsAsync();
            }
            catch (Exception e)
            {
                Logger.LogError($"Error scraping and syncing metrics to IoTHub - {e}");
            }
        }
    }
}
