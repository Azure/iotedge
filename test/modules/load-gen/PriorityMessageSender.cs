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

    class PriorityMessageSender : LoadGenSenderBase
    {
        // readonly string[] outputs = new string[] { "pri0", "pri1", "pri2", "pri3" };
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
            string priorityString = Settings.Current.Priorities.Expect(() =>
                new ArgumentException("PriorityMessageSender must have 'priorities' environment variable set to a valid list of string delimited by ';'"));
            string[] outputs = priorityString
                .Split(';')
                .Select(x => "pri" + x)
                .ToArray();

            bool firstMessageWhileOffline = true;
            var priorityAndSequence = new SortedDictionary<int, List<long>>();
            long messageIdCounter = 1;
            while (!cts.IsCancellationRequested &&
                (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
            {
                try
                {
                    int choosePri = this.rng.Next(4);
                    string output = outputs[choosePri];

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
                        int priority = int.Parse(output.Substring(3));
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
                .SelectMany(t =>
                {
                    t.Value.Sort();
                    return t.Value;
                })
                .ToList();

            // See explanation above why we need to send sequence number 1 first
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
