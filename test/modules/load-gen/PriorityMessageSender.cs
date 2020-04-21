// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class PriorityMessageSender : LoadGenSenderBase
    {
        readonly Random rng = new Random();
        private bool isFinished;
        private int resultsSent;

        public PriorityMessageSender(
            ILogger logger,
            ModuleClient moduleClient,
            Guid batchId,
            string trackingId)
            : base(logger, moduleClient, batchId, trackingId)
        {
            this.Priorities = Settings.Current.Priorities.Expect(() =>
                new ArgumentException("PriorityMessageSender must have 'priorities' environment variable set to a valid list of strings delimited by ';'"));
            this.Ttls = Settings.Current.Ttls.Expect(() =>
                new ArgumentException("PriorityMessageSender must have 'ttls' environment variable set to a valid list of strings delimited by ';'"));
            this.TtlThresholdSecs = Settings.Current.TtlThresholdSecs.Expect(() =>
                new ArgumentException("PriorityMessageSender must have 'ttlThresholdSecs' environment variable set to a valid int"));
            this.isFinished = false;
            this.resultsSent = 0;
        }

        List<int> Priorities { get; }

        List<int> Ttls { get; }

        int TtlThresholdSecs { get; }

        public async override Task RunAsync(CancellationTokenSource cts, DateTime testStartAt)
        {
            bool firstMessageWhileOffline = true;
            var priorityAndSequence = new SortedDictionary<int, List<long>>();
            long messageIdCounter = 1;
            string trcUrl = Settings.Current.TestResultCoordinatorUrl.Expect<ArgumentException>(() => throw new ArgumentException("Expected TestResultCoordinator URL"));

            await this.SetIsFinishedDirectMethodAsync();

            while (!cts.IsCancellationRequested &&
                (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
            {
                try
                {
                    int rand = this.rng.Next(this.Priorities.Count);
                    int priority = this.Priorities[rand];
                    int ttlForMessage = this.Ttls[rand];

                    await this.SendEventAsync(messageIdCounter, "pri" + priority);
                    this.Logger.LogInformation($"Sent message {messageIdCounter} with pri {priority} and ttl {ttlForMessage}");

                    // We need to set the first message because of the way priority queue logic works
                    // When edgeHub cannot send a message, it will retry on that message until it sends, regardless
                    // of priority and TTL. So even though it may not be highest priority or may be expired (or both),
                    // this message will still be the first to send when the receiver comes online.
                    if (firstMessageWhileOffline)
                    {
                        firstMessageWhileOffline = false;
                    }
                    else if (ttlForMessage <= 0 || ttlForMessage > this.TtlThresholdSecs)
                    {
                        priority = (priority < 0) ? 2000000000 : priority;

                        if (!priorityAndSequence.TryGetValue(priority, out List<long> sequenceNums))
                        {
                            priorityAndSequence.Add(priority, new List<long> { messageIdCounter });
                        }
                        else
                        {
                            sequenceNums.Add(messageIdCounter);
                        }
                    }

                    if (messageIdCounter % 1000 == 0)
                    {
                        this.Logger.LogInformation($"Sent {messageIdCounter} messages.");
                    }

                    messageIdCounter++;
                    await Task.Delay(Settings.Current.MessageFrequency);
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageIdCounter}, BatchId: {this.BatchId};");
                }
            }

            this.Logger.LogInformation($"Sending finished. Now sending expected results to {Settings.Current.TestResultCoordinatorUrl}");

            // Sort priority by sequence number
            List<long> expectedSequenceNumberList = priorityAndSequence
                .SelectMany(t => t.Value)
                .ToList();

            // Need to add 1 for the first sequence number, since it is a special case that we omitted in the expectedSequenceNumberList.
            this.resultsSent = expectedSequenceNumberList.Count + 1;

            // See explanation above why we need to send sequence number 1 as the first expected value.
            await this.ReportResult(1);

            foreach (int sequenceNumber in expectedSequenceNumberList)
            {
                // Report sending message successfully to Test Result Coordinator
                this.Logger.LogInformation($"Sending sequence number {sequenceNumber} to TRC");
                await this.ReportResult(sequenceNumber);
            }

            this.isFinished = true;
        }

        private async Task SetIsFinishedDirectMethodAsync()
        {
            await this.Client.SetMethodHandlerAsync(
                "IsFinished",
                async (MethodRequest methodRequest, object _) => await Task.FromResult(this.IsFinished()),
                null);
        }

        private MethodResponse IsFinished()
        {
            string response = JsonConvert.SerializeObject(new PriorityQueueTestStatus(this.isFinished, this.resultsSent));
            return new MethodResponse(Encoding.UTF8.GetBytes(response), (int)HttpStatusCode.OK);
        }
    }
}
