// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    class PriorityMessageSender : SenderBase
    {
        readonly string[] outputs = new string[] { "pri0", "pri1", "pri2", "pri3" };

        readonly Random rng = new Random();

        public PriorityMessageSender(
            ILogger logger,
            ModuleClient moduleClient,
            Guid batchId,
            string trackingId)
            : base(logger, moduleClient, batchId, trackingId)
        {
        }

        public async override Task RunAsync(CancellationTokenSource cts, DateTime testStartAt)
        {
            bool firstMessageWhileOffline = true;
            var priorityAndSequenceList = new List<(int, long)>();
            long messageIdCounter = 1;
            while (!cts.IsCancellationRequested &&
                (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
            {
                try
                {
                    int priority = this.rng.Next(4);
                    string output = this.outputs[priority];

                    await this.SendEventAsync(messageIdCounter, output);

                    // We need to set the first message because of the way priority queue logic works
                    // When edgeHub cannot send a message, it will retry on that message until it sends
                    // So even though it may not be highest priority, this message will still be the first
                    // to send when the receiver comes online
                    if (firstMessageWhileOffline)
                    {
                        firstMessageWhileOffline = false;
                    }
                    else
                    {
                        priorityAndSequenceList.Add((priority, messageIdCounter));
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
                    this.Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageIdCounter}, BatchId: {this.BatchId.ToString()};");
                }
            }

            this.Logger.LogInformation($"Sending finished. Now sending expected results to {Settings.Current.TestResultCoordinatorUrl}");

            // Sort by priority then sequence number. Then, select just the sequence numbers
            List<long> expectedSequenceNumberList = priorityAndSequenceList
                .OrderBy(t => t.Item1)
                .ThenBy(t => t.Item2)
                .Select(t => t.Item2)
                .ToList();

            await this.ReportResult(1);

            foreach (int sequenceNumber in expectedSequenceNumberList)
            {
                // Report sending message successfully to Test Result Coordinator
                this.Logger.LogInformation($"Sending sequence number {sequenceNumber} to TRC");
                await this.ReportResult(sequenceNumber);
            }
        }
    }
}
