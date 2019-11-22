// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Extensions.Logging;

    class LogAnalyticsMetricsSync : IMetricsSync
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        readonly MessageFormatter messageFormatter;
        readonly Scraper scraper;

        public LogAnalyticsMetricsSync(MessageFormatter messageFormatter, Scraper scraper)
        {
            this.messageFormatter = Preconditions.CheckNotNull(messageFormatter);
            this.scraper = Preconditions.CheckNotNull(scraper);
        }

        public async Task ScrapeAndSyncMetricsAsync()
        {
            try
            {
                IEnumerable<string> scrapedMetrics = await this.scraper.Scrape();
                foreach (string scrape in scrapedMetrics)
                {
                    string workspaceId = Settings.Current.AzMonWorkspaceId;
                    string workspaceKey = Settings.Current.AzMonWorkspaceKey;
                    string logType = Settings.Current.AzMonLogType;
                    AzureLogAnalytics.Instance.PostAsync(workspaceId, workspaceKey, this.messageFormatter.BuildJSON(scrape), logType);
                }

                Logger.LogInformation($"Sent metrics to LogAnalytics");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error scraping and syncing metrics - {e}");
            }
        }
    }
}
