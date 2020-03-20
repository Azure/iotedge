// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class DefaultSender : SenderBase
    {
        public DefaultSender(ILogger logger,
            ModuleClient moduleClient,
            Guid batchId,
            string trackingId)
            : base(logger, moduleClient, batchId, trackingId)
        {
        }

        public async override void StartAsync(CancellationTokenSource cts, DateTime testStartAt)
        {
            long messageIdCounter = 1;
            while (!cts.IsCancellationRequested &&
                (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
            {
                try
                {
                    await this.SendEventAsync(messageIdCounter, Settings.Current.OutputName);

                    // Report sending message successfully to Test Result Coordinator
                    await this.ReportResult(messageIdCounter);

                    if (messageIdCounter % 1000 == 0)
                    {
                        this.Logger.LogInformation($"Sent {messageIdCounter} messages.");
                    }

                    await Task.Delay(Settings.Current.MessageFrequency);
                    messageIdCounter++;
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageIdCounter}, BatchId: {this.BatchId.ToString()};");
                }
            }
        }
    }
}
