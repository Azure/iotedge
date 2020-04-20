// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
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
            this.PriorityString = Settings.Current.Priorities.Expect(() =>
                new ArgumentException("PriorityMessageSender must have 'priorities' environment variable set to a valid list of string delimited by ';'"));
            this.isFinished = false;
            this.resultsSent = 0;
        }

        public string PriorityString { get; }

        public async override Task RunAsync(CancellationTokenSource cts, DateTime testStartAt)
        {
            string[] outputs = this.PriorityString.Split(';');

            bool firstMessageWhileOffline = true;
            var priorityAndSequence = new SortedDictionary<int, List<long>>();
            long messageIdCounter = 1;

            await this.SetIsFinishedDirectMethodAsync();

            while (!cts.IsCancellationRequested &&
                (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
            {
                try
                {
                    int choosePri = this.rng.Next(outputs.Length);
                    string output = outputs[choosePri];

                    await this.SendEventAsync(messageIdCounter, "pri" + output);

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
                        int priority = 2000000000; // Default priority
                        if (!output.Contains(TestConstants.PriorityQueues.Default))
                        {
                            priority = int.Parse(output);
                        }

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

            if (priorityAndSequence.Keys.Count <= 1)
            {
                throw new InvalidOperationException($"Must send more than 1 priority for valid test results. Priorities sent: {priorityAndSequence.Keys.Count}");
            }

            // Sort priority by sequence number
            List<long> expectedSequenceNumberList = priorityAndSequence
                .SelectMany(t => t.Value)
                .ToList();

            this.resultsSent = expectedSequenceNumberList.Count;

            // See explanation above why we need to send sequence number 1 first
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
