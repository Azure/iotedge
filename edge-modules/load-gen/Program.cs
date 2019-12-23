// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("LoadGen");

        static async Task Main()
        {
            Logger.LogInformation($"Starting load gen with the following settings:\r\n{Settings.Current}");

            ModuleClient moduleClient = null;

            try
            {
                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                Guid batchId = Guid.NewGuid();
                Logger.LogInformation($"Batch Id={batchId}");

                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                Logger.LogInformation($"Load gen delay start for {Settings.Current.TestStartDelay}.");
                await Task.Delay(Settings.Current.TestStartDelay);

                DateTime testStartAt = DateTime.UtcNow;
                long messageIdCounter = 1;
                while (!cts.IsCancellationRequested &&
                    (Settings.Current.TestDuration == TimeSpan.Zero || DateTime.UtcNow - testStartAt < Settings.Current.TestDuration))
                {
                    try
                    {
                        await SendEventAsync(moduleClient, batchId, Settings.Current.TrackingId, messageIdCounter);

                        // Report sending message successfully to Test Result Coordinator
                        await Settings.Current.TestResultCoordinatorUrl.ForEachAsync(
                            async trcUrl =>
                            {
                                Uri testResultCoordinatorUrl = new Uri(
                                    trcUrl,
                                    UriKind.Absolute);
                                TestResultCoordinatorClient trcClient = new TestResultCoordinatorClient { BaseUrl = testResultCoordinatorUrl.AbsoluteUri };

                                await ModuleUtil.ReportStatus(
                                    trcClient,
                                    Logger,
                                    Settings.Current.ModuleId + ".send",
                                    ModuleUtil.FormatMessagesTestResultValue(
                                        Settings.Current.TrackingId,
                                        batchId.ToString(),
                                        messageIdCounter.ToString()),
                                    TestOperationResultType.Messages.ToString());
                            });

                        if (messageIdCounter % 1000 == 0)
                        {
                            Logger.LogInformation($"Sent {messageIdCounter} messages.");
                        }

                        await Task.Delay(Settings.Current.MessageFrequency);
                        messageIdCounter++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, $"[SendEventAsync] Sequence number {messageIdCounter}, BatchId: {batchId.ToString()};");
                    }
                }

                Logger.LogInformation("Finish sending messages.");
                await cts.Token.WhenCanceled();
                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred during load gen.");
            }
            finally
            {
                Logger.LogInformation("Closing connection to Edge Hub.");
                moduleClient?.CloseAsync();
                moduleClient?.Dispose();
            }

            Logger.LogInformation("Load Gen complete. Exiting.");
        }

        static async Task SendEventAsync(ModuleClient client, Guid batchId, string trackingId, long messageId)
        {
            var random = new Random();
            var bufferPool = new BufferPool();

            using (Buffer data = bufferPool.AllocBuffer(Settings.Current.MessageSizeInBytes))
            {
                // generate some bytes
                random.NextBytes(data.Data);

                // build message
                var messageBody = new { data = data.Data };
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                message.Properties.Add(TestConstants.Message.SequenceNumberPropertyName, messageId.ToString());
                message.Properties.Add(TestConstants.Message.BatchIdPropertyName, batchId.ToString());
                message.Properties.Add(TestConstants.Message.TrackingIdPropertyName, trackingId);

                // sending the result via edgeHub
                await client.SendEventAsync(Settings.Current.OutputName, message);
                Logger.LogInformation($"Sent message successfully: sequenceNumber={messageId}");
            }
        }
    }
}
