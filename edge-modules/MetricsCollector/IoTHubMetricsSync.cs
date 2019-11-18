namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public class IoTHubMetricsSync : IMetricsSync
    {
        readonly MessageFormatter messageFormatter;
        readonly Scraper scraper;
        readonly ModuleClient moduleClient;

        public IoTHubMetricsSync(MessageFormatter messageFormatter, Scraper scraper, ModuleClient moduleClient)
        {
            this.messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
            this.scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
            this.moduleClient = moduleClient; 
        }

        public async Task ScrapeAndSync()
        {
            try
            {
                IEnumerable<string> scrapedMetrics = await this.scraper.Scrape();
                IList<Message> messages =
                        scrapedMetrics.SelectMany(prometheusMetrics => this.messageFormatter.Build(prometheusMetrics)).ToList();
                await this.moduleClient.SendEventBatchAsync(messages);
                Console.WriteLine($"Sent metrics as {messages.Count} messages");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error scraping and syncing metrics - {e}");
            }
        }
    }    
}