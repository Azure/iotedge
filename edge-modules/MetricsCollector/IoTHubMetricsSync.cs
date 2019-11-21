// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class IoTHubMetricsSync : IMetricsSync
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("MetricsCollector");
        readonly MessageFormatter messageFormatter;
        readonly Scraper scraper;
        readonly ModuleClient moduleClient;

        public IoTHubMetricsSync(MessageFormatter messageFormatter, Scraper scraper, ModuleClient moduleClient)
        {
            this.messageFormatter = Preconditions.CheckNotNull(messageFormatter);
            this.scraper = Preconditions.CheckNotNull(scraper);
            this.moduleClient = Preconditions.CheckNotNull(moduleClient);
        }

        public async Task ScrapeAndSync()
        {
            try
            {
                IEnumerable<string> scrapedMetrics = await this.scraper.Scrape();
                IList<Message> messages =
                        scrapedMetrics.SelectMany(prometheusMetrics => this.messageFormatter.Build(prometheusMetrics)).ToList();
                await this.moduleClient.SendEventBatchAsync(messages);
                Logger.LogInformation($"Sent metrics as {messages.Count} messages");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error scraping and syncing metrics - {e}");
            }
        }
    }
}
