// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;

    public class ValidateNumberOfMessagesSent
    {
        ModuleClient moduleClient;
        IMetricsScraper scraper;

        public ValidateNumberOfMessagesSent(ModuleClient moduleClient, IMetricsScraper scraper)
        {
            this.moduleClient = moduleClient;
            this.scraper = scraper;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            int prevSent = await this.GetNumberOfMessagesSent(cancellationToken);
            await this.SendMessages(10, cancellationToken);
            int newSent = await this.GetNumberOfMessagesSent(cancellationToken);

            Console.WriteLine(newSent - prevSent == 10);
        }

        async Task<int> GetNumberOfMessagesSent(CancellationToken cancellationToken)
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            Metric metric = metrics.First(m => m.Name == "edgehub_messages_received_total" && m.Tags.TryGetValue("id", out string id) && id.Contains("MetricsValidator"));

            return (int?)metric?.Value ?? 0;
        }

        Task SendMessages(int n, CancellationToken cancellationToken)
        {
            var messagesToSend = Enumerable.Range(1, n).Select(i => new Message(Encoding.UTF8.GetBytes($"Message {i}")));
            return Task.WhenAll(messagesToSend.Select(m => this.moduleClient.SendEventAsync("ValidationOutput", m, cancellationToken)));
        }
    }
}
