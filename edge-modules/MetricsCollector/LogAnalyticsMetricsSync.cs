namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class LogAnalyticsMetricsSync : IMetricsSync
    {
        readonly MessageFormatter messageFormatter;
        readonly Scraper scraper;
        readonly AzureLogAnalytics logAnalytics;

        public LogAnalyticsMetricsSync(MessageFormatter messageFormatter, Scraper scraper, AzureLogAnalytics logAnalytics)
        {
            this.messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
            this.scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
            this.logAnalytics = logAnalytics;
        }

        public async Task ScrapeAndSync()
        {
            try
            {
                IEnumerable<string> scrapedMetrics = await this.scraper.Scrape();
                foreach (var scrape in scrapedMetrics)
                {
                    logAnalytics.Post(this.messageFormatter.BuildJSON(scrape));
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
