// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MetricsValidator.Util;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.WindowsAzure.Storage.Table;

    public class ValidateMessages : TestBase
    {
        TransportType transportType;

        public ValidateMessages(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient, TransportType transportType)
            : base(testReporter, scraper, moduleClient)
        {
            this.transportType = transportType;
        }

        protected override async Task Test(CancellationToken cancellationToken)
        {
            // This must be first, since message size is only recorded per module
            /* Currently, this is disabled. Since message size only reports size per module, any message sent messes up this test.
             * await this.TestMessageSize(cancellationToken);
             */

            await this.TestMessages(cancellationToken);
            await this.TestQueueLength(cancellationToken);
        }

        /*******************************************************************************
         * Tests
         * *****************************************************************************/
        async Task TestQueueLength(CancellationToken cancellationToken)
        {
            TestReporter reporter = this.testReporter.MakeSubcategory("Queue Length");
            using (reporter.MeasureDuration())
            {
                const string input = "FromSelf";
                const string output = "ToSelf";

                // Setup reciever
                TaskCompletionSource<MessageResponse> tcs = new TaskCompletionSource<MessageResponse>();
                tcs.SetResult(MessageResponse.Completed);
                await this.moduleClient.SetInputMessageHandlerAsync(input, (message, _) => tcs.Task, null, cancellationToken);

                // This will assert the queue clears
                async Task WaitForQueueToClear(string name)
                {
                    TimeSpan maxWaitTime = TimeSpan.FromSeconds(10);
                    TimeSpan frequency = TimeSpan.FromMilliseconds(100);

                    int queueLength = await this.GetQueueLength(cancellationToken, input);
                    for (int i = 0; i < maxWaitTime / frequency; i++)
                    {
                        if (queueLength == 0)
                        {
                            reporter.Assert(name, true);
                            return;
                        }

                        await Task.Delay(frequency);
                        queueLength = await this.GetQueueLength(cancellationToken, input);
                    }

                    reporter.Assert(name, false, $"After waiting {maxWaitTime.Seconds} seconds, queue length was {queueLength}");
                }

                // Begin tests
                reporter.Assert("Queue starts empty", 0, await this.GetQueueLength(cancellationToken, input));

                async Task FillAndEmptyQueue(int n)
                {
                    tcs = new TaskCompletionSource<MessageResponse>();
                    await this.SendMessages(n, cancellationToken, endpoint: output);
                    reporter.Assert($"Empty Queue Test: Queue increases to {n}", n, await this.GetQueueLength(cancellationToken, input));

                    tcs.SetResult(MessageResponse.Completed);
                    await WaitForQueueToClear($"Queue empties from {n}");
                }

                await FillAndEmptyQueue(10);
                await FillAndEmptyQueue(100);

                async Task FillAndAbandon(int n)
                {
                    tcs = new TaskCompletionSource<MessageResponse>();
                    await this.SendMessages(n, cancellationToken, endpoint: output);
                    reporter.Assert($"Abandon Queue Test: Queue increases to {n}", n, await this.GetQueueLength(cancellationToken, input));

                    tcs.SetResult(MessageResponse.Abandoned);
                    await WaitForQueueToClear($"Queue is empty when abandoned from {n}");
                }

                // MQTT doesn't support abandoning
                if (!new TransportType[] { TransportType.Mqtt, TransportType.Mqtt_Tcp_Only, TransportType.Mqtt_WebSocket_Only }.Contains(this.transportType))
                {
                    await FillAndAbandon(10);
                    await FillAndAbandon(100);
                }

                async Task FillAndEmptyBatch(int n, int m)
                {
                    tcs = new TaskCompletionSource<MessageResponse>();
                    await this.SendMessageBatches(n, m, cancellationToken, endpoint: output);
                    reporter.Assert($"Empty Queue Test: Queue increases to {n * m} for {n} batches of {m}", n * m, await this.GetQueueLength(cancellationToken, input));

                    tcs.SetResult(MessageResponse.Completed);
                    await WaitForQueueToClear($"Queue empties from {n * m} for {n} batches of {m}");
                }

                await FillAndEmptyBatch(2, 5);
                await FillAndEmptyBatch(10, 10);
            }
        }

        async Task TestMessageSize(CancellationToken cancellationToken)
        {
            string endpoint = Guid.NewGuid().ToString();

            TestReporter reporter = this.testReporter.MakeSubcategory("Message Size");
            using (reporter.MeasureDuration())
            {
                await this.SendMessages(100, cancellationToken, endpoint, 1000);
                (await this.GetMessageSize(cancellationToken))
                     .ForEach(
                         size =>
                             {
                                 reporter.Assert("Sum is correct", size.Sum, 100000);
                                 reporter.Assert("Count is correct", size.Sum, 100);
                                 reporter.Assert("All quartiles have same size", size.Quartiles.Values.All(s => s == 1000));
                             },
                         () => reporter.Assert("All same size", false, "Could not get message size"));

                await this.SendMessages(50, cancellationToken, endpoint, 10);
                (await this.GetMessageSize(cancellationToken))
                     .ForEach(
                         size =>
                         {
                             reporter.Assert("High median is correct", size.Quartiles[".5"] == 1000);
                         },
                         () => reporter.Assert("High median", false, "Could not get message size"));

                await this.SendMessages(100, cancellationToken, endpoint, 10);
                (await this.GetMessageSize(cancellationToken))
                     .ForEach(
                         size =>
                         {
                             reporter.Assert("Low median is correct", size.Quartiles[".5"] == 1000);
                         },
                         () => reporter.Assert("Low median", false, "Could not get message size"));

                await this.SendMessages(1, cancellationToken, endpoint, 10000);
                (await this.GetMessageSize(cancellationToken))
                     .ForEach(
                         size =>
                         {
                             reporter.Assert("Top quartiles are correct", size.Quartiles[".9999"] == 10000 && size.Quartiles[".999"] == 10000);
                         },
                         () => reporter.Assert("one bigger", false, "Could not get message size"));
            }
        }

        async Task TestMessages(CancellationToken cancellationToken)
        {
            string endpoint = Guid.NewGuid().ToString();
            TimeSpan timePerMessage = TimeSpan.FromMilliseconds(100);

            TestReporter reporter = this.testReporter.MakeSubcategory("Messages Sent and Recieved");
            using (reporter.MeasureDuration())
            {
                async Task CountSingleSends(int n)
                {
                    int prevSent = await this.GetNumberOfMessagesSent(cancellationToken, endpoint);
                    int prevRecieved = await this.GetNumberOfMessagesRecieved(cancellationToken, endpoint);
                    await this.SendMessages(n, cancellationToken, endpoint);

                    await Task.Delay(timePerMessage * n + TimeSpan.FromMilliseconds(1000)); // Give edgeHub time to send message upstream
                    int newSent = await this.GetNumberOfMessagesSent(cancellationToken, endpoint);
                    int newRecieved = await this.GetNumberOfMessagesRecieved(cancellationToken, endpoint);

                    reporter.Assert($"Reports {n} messages recieved", n, newRecieved - prevRecieved);
                    reporter.Assert($"Reports {n} messages sent", n, newSent - prevSent);

                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }

                await Task.Delay(TimeSpan.FromMilliseconds(2000));
                await CountSingleSends(1);
                await CountSingleSends(10);
                await CountSingleSends(100);

                async Task CountMultipleSends(int n, int m)
                {
                    int prevSent = await this.GetNumberOfMessagesSent(cancellationToken, endpoint);
                    int prevRecieved = await this.GetNumberOfMessagesRecieved(cancellationToken, endpoint);
                    await this.SendMessageBatches(n, m, cancellationToken, endpoint);

                    await Task.Delay(timePerMessage * n * m + TimeSpan.FromMilliseconds(1000)); // Give edgeHub time to send message upstream
                    int newSent = await this.GetNumberOfMessagesSent(cancellationToken, endpoint);
                    int newRecieved = await this.GetNumberOfMessagesRecieved(cancellationToken, endpoint);

                    reporter.Assert($"Reports {n * m} recieved for {n} batches of {m}", n * m, newRecieved - prevRecieved);
                    reporter.Assert($"Reports {n * m} sent for {n} batches of {m}", n * m, newSent - prevSent);

                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }

                await CountMultipleSends(1, 1);
                await CountMultipleSends(10, 1);
                await CountMultipleSends(1, 10);
                await CountMultipleSends(10, 10);
            }
        }

        /*******************************************************************************
         * Helpers
         * *****************************************************************************/
        async Task<int> GetNumberOfMessagesRecieved(CancellationToken cancellationToken, string endpoint)
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            Metric metric = metrics.FirstOrDefault(m => m.Name == "edgehub_messages_received_total" && m.Tags.TryGetValue("route_output", out string output) && output == endpoint);

            return (int?)metric?.Value ?? 0;
        }

        async Task<int> GetNumberOfMessagesSent(CancellationToken cancellationToken, string endpoint)
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            Metric metric = metrics.FirstOrDefault(m => m.Name == "edgehub_messages_sent_total" && m.Tags.TryGetValue("from_route_output", out string output) && output == endpoint);

            return (int?)metric?.Value ?? 0;
        }

        async Task<int> GetQueueLength(CancellationToken cancellationToken, string endpoint)
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            Metric metric = metrics.FirstOrDefault(m => m.Name == "edgehub_queue_length" && m.Tags.TryGetValue("endpoint", out string output) && output.Contains(endpoint));

            return (int?)metric?.Value ?? 0;
        }

        async Task<Option<HistogramQuartiles>> GetMessageSize(CancellationToken cancellationToken, string moduleName = "MetricsValidator")
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            metrics = metrics.Where(m => m.Tags["id"].Contains(moduleName));
            return HistogramQuartiles.CreateFromMetrics(metrics, "edgehub_message_size_bytes");
        }

        Task SendMessages(int n, CancellationToken cancellationToken, string endpoint, int messageSize = 10)
        {
            var messagesToSend = Enumerable.Range(1, n).Select(i => new Message(new byte[messageSize]));
            return Task.WhenAll(messagesToSend.Select(m => this.moduleClient.SendEventAsync(endpoint, m, cancellationToken)));
        }

        Task SendMessageBatches(int n, int m, CancellationToken cancellationToken, string endpoint, int messageSize = 10)
        {
            return Task.WhenAll(Enumerable.Range(1, n).Select(i => this.moduleClient.SendEventBatchAsync(endpoint, Enumerable.Range(1, m).Select(j => new Message(new byte[messageSize])), cancellationToken)));
        }
    }
}
