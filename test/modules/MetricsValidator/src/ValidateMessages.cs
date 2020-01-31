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
    using Microsoft.Extensions.Logging;

    public class ValidateMessages : TestBase
    {
        readonly string endpoint = Guid.NewGuid().ToString();

        public ValidateMessages(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
            : base(testReporter, scraper, moduleClient)
        {
        }

        public override async Task Start(CancellationToken cancellationToken)
        {
            log.LogInformation($"Starting {nameof(ValidateMessages)}");
            await this.TestNumberSent(10, cancellationToken);
            await this.TestNumberSent(100, cancellationToken);

            await this.TestBatch(1, 1, cancellationToken);
            await this.TestBatch(10, 1, cancellationToken);
            await this.TestBatch(1, 10, cancellationToken);
            await this.TestBatch(10, 10, cancellationToken);

            await this.TestMessageSize(cancellationToken);
        }

        /*******************************************************************************
         * Tests
         * *****************************************************************************/
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

        async Task TestMessageSize(CancellationToken cancellationToken)
        {
            string endpoint = Guid.NewGuid().ToString();

            await this.SendMessages(10, cancellationToken, 250, endpoint);
        }

        /*******************************************************************************
         * Helpers
         * *****************************************************************************/
        async Task<int> GetNumberOfMessagesSent(CancellationToken cancellationToken, string endpoint = null)
        {
            var metrics = (await this.scraper.ScrapeEndpointsAsync(cancellationToken)).ToArray();
            Metric metric = metrics.FirstOrDefault(m => m.Name == "edgehub_messages_received_total" && m.Tags.TryGetValue("route_output", out string output) && output == (endpoint ?? this.endpoint));

            return (int?)metric?.Value ?? 0;
        }

        async Task<HistogramQuartiles> GetMessageSize(CancellationToken cancellationToken, string endpoint = null)
        {
            var metrics = (await this.scraper.ScrapeEndpointsAsync(cancellationToken)).ToArray();
            Metric metric = metrics.Select(m => m.Name == "edgehub_message_size_bytes" && m.Tags.TryGetValue("route_output", out string output) && output == (endpoint ?? this.endpoint));

        }

        Task SendMessages(int n, CancellationToken cancellationToken, int messageSize = 10, string endpoint = null)
        {
            var messagesToSend = Enumerable.Range(1, n).Select(i => new Message(new byte[messageSize]));
            return Task.WhenAll(messagesToSend.Select(m => this.moduleClient.SendEventAsync(endpoint ?? this.endpoint, m, cancellationToken)));
        }

        Task SendMessageBatches(int n, int m, CancellationToken cancellationToken, int messageSize = 10, string endpoint = null)
        {
            return Task.WhenAll(Enumerable.Range(1, n).Select(i => this.moduleClient.SendEventBatchAsync(endpoint ?? this.endpoint, Enumerable.Range(1, m).Select(j => new Message(new byte[messageSize])), cancellationToken)));
        }

        class HistogramQuartiles
        {
            public double Q5;
            public double Q9;
            public double Q95;
            public double Q99;
            public double Q999;
            public double Q9999;
        }
    }
}
