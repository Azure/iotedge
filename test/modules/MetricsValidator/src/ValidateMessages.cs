// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Util;
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

            // This must be first, since message size is only recorded per module
            await this.TestMessageSize(cancellationToken);

            await this.CountSingleSends(10, cancellationToken);
            await this.CountSingleSends(100, cancellationToken);

            await this.CountMultipleSends(1, 1, cancellationToken);
            await this.CountMultipleSends(10, 1, cancellationToken);
            await this.CountMultipleSends(1, 10, cancellationToken);
            await this.CountMultipleSends(10, 10, cancellationToken);
        }

        /*******************************************************************************
         * Tests
         * *****************************************************************************/
        async Task TestMessageSize(CancellationToken cancellationToken)
        {
            await this.SendMessages(10, cancellationToken, 250);
            (await this.GetMessageSize(cancellationToken))
                 .ForEach(
                     size =>
                         {
                             this.testReporter.Assert("Sum is correct", size.Sum, 2500);
                             this.testReporter.Assert("Count is correct", size.Sum, 2500);
                             this.testReporter.Assert("All quartiles have same size", size.Quartiles.Values.All(s => s == 250));
                         },
                     () => this.testReporter.Assert("Message Size", false, "Could not get message size"));
        }

        async Task CountSingleSends(int n, CancellationToken cancellationToken)
        {
            int prevSent = await this.GetNumberOfMessagesSent(cancellationToken);
            await this.SendMessages(n, cancellationToken);
            int newSent = await this.GetNumberOfMessagesSent(cancellationToken);
            this.testReporter.Assert($"Reports {n} messages sent", n, newSent - prevSent);
        }

        async Task CountMultipleSends(int n, int m, CancellationToken cancellationToken)
        {
            int prevSent = await this.GetNumberOfMessagesSent(cancellationToken);
            await this.SendMessageBatches(n, m, cancellationToken);
            int newSent = await this.GetNumberOfMessagesSent(cancellationToken);
            this.testReporter.Assert($"Reports {n * m} for {n} batches of {m}", n * m, newSent - prevSent);
        }

        /*******************************************************************************
         * Helpers
         * *****************************************************************************/
        async Task<int> GetNumberOfMessagesSent(CancellationToken cancellationToken, string endpoint = null)
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            Metric metric = metrics.FirstOrDefault(m => m.Name == "edgehub_messages_received_total" && m.Tags.TryGetValue("route_output", out string output) && output == (endpoint ?? this.endpoint));

            return (int?)metric?.Value ?? 0;
        }

        async Task<Option<HistogramQuartiles>> GetMessageSize(CancellationToken cancellationToken)
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            return HistogramQuartiles.CreateFromMetrics(metrics, "edgehub_message_size_bytes");
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
            public static readonly string[] QuartileNames = { ".5", ".9", ".95", ".99", ".999", ".9999" };

            public double Sum;
            public double Count;
            public Dictionary<string, double> Quartiles;

            public static Option<HistogramQuartiles> CreateFromMetrics(IEnumerable<Metric> metrics, string metricName)
            {
                var releventMetrics = metrics.Where(m => m.Name.Contains(metricName) && m.Tags["id"].Contains("MetricsValidator")).ToList();

                if (releventMetrics.Count != 8)
                {
                    return Option.None<HistogramQuartiles>();
                }

                try
                {
                    return Option.Some(new HistogramQuartiles
                    {
                        Quartiles = releventMetrics.Where(m => m.Name == metricName).ToDictionary(m => m.Tags["quantile"], m => m.Value),
                        Count = releventMetrics.First(m => m.Name == $"{metricName}_count").Value,
                        Sum = releventMetrics.First(m => m.Name == $"{metricName}_sum").Value,
                    });
                }
                catch
                {
                    return Option.None<HistogramQuartiles>();
                }
            }
        }
    }
}
