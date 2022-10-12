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
    using ModuleClientWrapper;


    using System.Diagnostics;

    internal static class Program
    {
        private static void WaitForDebugger()
        {
            LoggerUtil.Writer.LogInformation("Waiting for debugger to attach");
            for (int i = 0; i < 300 && !Debugger.IsAttached; i++)
            {
                Thread.Sleep(100);
            }
            Thread.Sleep(250);
        }

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            LoggerUtil.Writer.LogInformation("Initializing metrics collector");
            LoggerUtil.Writer.LogInformation("Version - {0}", Settings.Current.Version);

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), LoggerUtil.Writer);

            // wait up to 30 seconds for debugger to attach if in a debug build
#if DEBUG
            WaitForDebugger();
#endif

            LoggerUtil.Writer.LogInformation($"Metrics collector initialized with the following settings:\r\n{Settings.Information}");
            IModuleClientWrapper moduleClientWrapper = null;
            try
            {
                moduleClientWrapper = await BuildModuleClientWrapperAsync(Settings.Current.UploadTarget, cts);
                PeriodicTask periodicIothubConnect = new PeriodicTask(moduleClientWrapper.RecreateClientAsync, Settings.Current.IotHubConnectFrequency, TimeSpan.FromMinutes(1), LoggerUtil.Writer, "Reconnect to IoT Hub", true);

                MetricsScraper scraper = new MetricsScraper(Settings.Current.Endpoints);
                IMetricsPublisher publisher;
                if (Settings.Current.UploadTarget == UploadTarget.AzureMonitor)
                {
                    publisher = new FixedSetTableUpload.FixedSetTableUpload(Settings.Current.LogAnalyticsWorkspaceId, Settings.Current.LogAnalyticsWorkspaceKey);
                }
                else
                {
                    publisher = new IotHubMetricsUpload.IotHubMetricsUpload(moduleClientWrapper);
                }

                using (MetricsScrapeAndUpload metricsScrapeAndUpload = new MetricsScrapeAndUpload(scraper, publisher))
                {
                    TimeSpan scrapeAndUploadInterval = TimeSpan.FromSeconds(Settings.Current.ScrapeFrequencySecs);
                    metricsScrapeAndUpload.Start(scrapeAndUploadInterval);
                    // await cts.Token.WhenCanceled();
                    WaitHandle.WaitAny(new WaitHandle[] { cts.Token.WaitHandle });
                }
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError(e, "Error occurred during metrics collection setup.");
            }
            finally
            {
                ((IDisposable)moduleClientWrapper)?.Dispose();
            }

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));

            LoggerUtil.Writer.LogInformation("MetricsCollector Main() finished.");
            return 0;
        }

        static async Task<IModuleClientWrapper> BuildModuleClientWrapperAsync(UploadTarget uploadTarget, CancellationTokenSource cts)
        {
            if (uploadTarget == UploadTarget.AzureMonitor)
            {
                return await AzureMonitorClientWrapper.BuildModuleClientWrapperAsync();
            }
            else
            {
                return await IotMessageModuleClientWrapper.BuildModuleClientWrapperAsync(cts);
            }
        }
    }
}
