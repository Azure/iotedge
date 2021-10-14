// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    class DefaultMessageSender : LoadGenSenderBase
    {
        public DefaultMessageSender(
            ILogger logger,
            ModuleClient moduleClient,
            Guid batchId,
            string trackingId)
            : base(logger, moduleClient, batchId, trackingId)
        {
        }

        public async override Task RunAsync(CancellationTokenSource cts, DateTime testStartAt)
        {
            long messageIdCounter = 1;
            while (!cts.IsCancellationRequested &&
                (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
            {
                try
                {
                    await Task.Delay(Settings.Current.MessageFrequency);

                    using var activity = Settings.activitySource.StartActivity("RunLoadGenAsync", ActivityKind.Internal);

                    await this.SendEventAsync(messageIdCounter, Settings.Current.OutputName);

                    // Report sending message successfully to Test Result Coordinator
                    await this.ReportResult(messageIdCounter);

                    if (messageIdCounter % 1000 == 0)
                    {
                        this.Logger.LogInformation($"Sent {messageIdCounter} messages.");
                    }

                    activity?.SetTag("default.message.count", messageIdCounter);
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
