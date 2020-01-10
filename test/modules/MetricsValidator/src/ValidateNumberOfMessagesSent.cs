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
        TestReporter testReporter;
        ModuleClient moduleClient;
        IMetricsScraper scraper;

        public ValidateNumberOfMessagesSent(TestReporter testReporter, ModuleClient moduleClient, IMetricsScraper scraper)
        {
            this.testReporter = testReporter.MakeSubcategory("ValidateNumberOfMessagesSent");
            this.moduleClient = moduleClient;
            this.scraper = scraper;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
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
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            Metric metric = metrics.First(m => m.Name == "edgehub_messages_received_total" && m.Tags.TryGetValue("id", out string id) && id.Contains(Constants.SendEndpoint));

            return (int?)metric?.Value ?? 0;
        }

        Task SendMessages(int n, CancellationToken cancellationToken)
        {
            var messagesToSend = Enumerable.Range(1, n).Select(i => new Message(Encoding.UTF8.GetBytes($"Message {i}")));
            return Task.WhenAll(messagesToSend.Select(m => this.moduleClient.SendEventAsync(Constants.SendEndpoint, m, cancellationToken)));
        }
    }
}
