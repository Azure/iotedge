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
        readonly string endpoint = Guid.NewGuid().ToString();

        TestReporter testReporter;
        IMetricsScraper scraper;
        ModuleClient moduleClient;

        public ValidateNumberOfMessagesSent(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
        {
            this.testReporter = testReporter.MakeSubcategory(nameof(ValidateNumberOfMessagesSent));
            this.scraper = scraper;
            this.moduleClient = moduleClient;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            Console.WriteLine($"Starting {nameof(ValidateNumberOfMessagesSent)}");

            int prevSent = await this.GetNumberOfMessagesSent(cancellationToken);
            await this.SendMessages(10, cancellationToken);
            int newSent = await this.GetNumberOfMessagesSent(cancellationToken);
            this.testReporter.Assert("Reports 10 messages sent", 10, newSent - prevSent);

            prevSent = await this.GetNumberOfMessagesSent(cancellationToken);
            await this.SendMessages(20, cancellationToken);
            newSent = await this.GetNumberOfMessagesSent(cancellationToken);
            this.testReporter.Assert("Reports 20 messages sent", 20, newSent - prevSent);
        }

        async Task<int> GetNumberOfMessagesSent(CancellationToken cancellationToken)
        {
            var metrics = (await this.scraper.ScrapeEndpointsAsync(cancellationToken)).ToArray();
            Console.WriteLine($"Scraped {metrics.Length} metrics");
            Metric metric = metrics.FirstOrDefault(m => m.Name == "edgehub_messages_received_total" && m.Tags.TryGetValue("id", out string id) && id.Contains("MetricsValidator"));

            return (int?)metric?.Value ?? 0;
        }

        Task SendMessages(int n, CancellationToken cancellationToken)
        {
            var messagesToSend = Enumerable.Range(1, n).Select(i => new Message(Encoding.UTF8.GetBytes($"Message {i}")));
            return Task.WhenAll(messagesToSend.Select(m => this.moduleClient.SendEventAsync(m, cancellationToken)));
        }
    }
}
