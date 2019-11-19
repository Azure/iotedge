// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;

    class LogAnalyticsMetricsSync : IMetricsSync
    {
        readonly MessageFormatter messageFormatter;
        readonly Scraper scraper;

        public LogAnalyticsMetricsSync(MessageFormatter messageFormatter, Scraper scraper)
        {
            this.messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
            this.scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
        }

        public async Task ScrapeAndSync()
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

                Console.WriteLine($"Sent metrics to LogAnalytics");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error scraping and syncing metrics - {e}");
            }
        }
    }
}
