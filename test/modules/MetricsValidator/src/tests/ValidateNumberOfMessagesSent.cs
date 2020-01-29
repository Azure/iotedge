// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Extensions.Logging;

    public class ValidateNumberOfMessagesSent : TestBase
    {
        readonly string endpoint = Guid.NewGuid().ToString();

        public ValidateNumberOfMessagesSent(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
            : base(testReporter, scraper, moduleClient)
        {
        }

        protected override async Task Test(CancellationToken cancellationToken)
        {
            await this.TestNumberSent(10, cancellationToken);
            await this.TestNumberSent(100, cancellationToken);

            await this.TestBatch(1, 1, cancellationToken);
            await this.TestBatch(10, 1, cancellationToken);
            await this.TestBatch(1, 10, cancellationToken);
            await this.TestBatch(10, 10, cancellationToken);
        }

        async Task TestNumberSent(int n, CancellationToken cancellationToken)
        {
            int prevSent = await this.GetNumberOfMessagesSent(cancellationToken);
            await this.SendMessages(n, cancellationToken);
            int newSent = await this.GetNumberOfMessagesSent(cancellationToken);
            this.testReporter.Assert($"Reports {n} messages sent", n, newSent - prevSent);
        }

        async Task TestBatch(int n, int m, CancellationToken cancellationToken)
        {
            int prevSent = await this.GetNumberOfMessagesSent(cancellationToken);
            await this.SendMessageBatches(n, m, cancellationToken);
            int newSent = await this.GetNumberOfMessagesSent(cancellationToken);
            this.testReporter.Assert($"Reports {n * m} for {n} batches of {m}", n * m, newSent - prevSent);
        }

        async Task<int> GetNumberOfMessagesSent(CancellationToken cancellationToken)
        {
            var metrics = (await this.scraper.ScrapeEndpointsAsync(cancellationToken)).ToArray();
            Metric metric = metrics.FirstOrDefault(m => m.Name == "edgehub_messages_received_total" && m.Tags.TryGetValue("route_output", out string output) && output == this.endpoint);

            return (int?)metric?.Value ?? 0;
        }

        Task SendMessages(int n, CancellationToken cancellationToken)
        {
            var messagesToSend = Enumerable.Range(1, n).Select(i => new Message(Encoding.UTF8.GetBytes($"Message {i}")));
            return Task.WhenAll(messagesToSend.Select(m => this.moduleClient.SendEventAsync(this.endpoint, m, cancellationToken)));
        }

        Task SendMessageBatches(int n, int m, CancellationToken cancellationToken)
        {
            return Task.WhenAll(Enumerable.Range(1, n).Select(i => this.moduleClient.SendEventBatchAsync(this.endpoint, Enumerable.Range(1, m).Select(j => new Message(Encoding.UTF8.GetBytes($"Message {i}, {j}"))), cancellationToken)));
        }
    }
}
