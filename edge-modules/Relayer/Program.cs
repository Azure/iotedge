// Copyright (c) Microsoft. All rights reserved.
namespace Relayer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /*
     * Module for relaying messages. It receives a message and passes it on.
     */
    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("Relayer");

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting Relayer with the following settings: \r\n{Settings.Current}");
            ModuleClient moduleClient = null;

            try
            {
                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                // Receive a message and call ProcessAndSendMessageAsync to send it on its way
                await moduleClient.SetInputMessageHandlerAsync(Settings.Current.InputName, ProcessAndSendMessageAsync, moduleClient);

                await cts.Token.WhenCanceled();
                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("Relayer Main() finished.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred during Relayer.");
            }
            finally
            {
                moduleClient?.CloseAsync();
                moduleClient?.Dispose();
            }
        }

        static async Task<MessageResponse> ProcessAndSendMessageAsync(Message message, object userContext)
        {
            Uri testResultCoordinatorUrl = Settings.Current.TestResultCoordinatorUrl;
            try
            {
                if (!(userContext is ModuleClient moduleClient))
                {
                    throw new InvalidOperationException("UserContext doesn't contain expected value");
                }

                // Must make a new message instead of reusing the old message because of the way the SDK sends messages
                string trackingId = string.Empty;
                string batchId = string.Empty;
                string sequenceNumber = string.Empty;
                var messageProperties = new List<KeyValuePair<string, string>>();

                foreach (KeyValuePair<string, string> prop in message.Properties)
                {
                    switch (prop.Key)
                    {
                        case TestConstants.Message.TrackingIdPropertyName:
                            trackingId = prop.Value ?? string.Empty;
                            break;
                        case TestConstants.Message.BatchIdPropertyName:
                            batchId = prop.Value ?? string.Empty;
                            break;
                        case TestConstants.Message.SequenceNumberPropertyName:
                            sequenceNumber = prop.Value ?? string.Empty;
                            break;
                    }

                    messageProperties.Add(new KeyValuePair<string, string>(prop.Key, prop.Value));
                }

                if (string.IsNullOrWhiteSpace(trackingId) || string.IsNullOrWhiteSpace(batchId) || string.IsNullOrWhiteSpace(sequenceNumber))
                {
                    Logger.LogWarning($"Received message missing info: trackingid={trackingId}, batchId={batchId}, sequenceNumber={sequenceNumber}");
                    return MessageResponse.Completed;
                }

                // Report receiving message successfully to Test Result Coordinator
                TestResultCoordinatorClient trcClient = new TestResultCoordinatorClient { BaseUrl = testResultCoordinatorUrl.AbsoluteUri };
                await ModuleUtil.ReportStatus(
                    trcClient,
                    Logger,
                    Settings.Current.ModuleId + ".receive",
                    ModuleUtil.FormatMessagesTestResultValue(trackingId, batchId, sequenceNumber),
                    TestOperationResultType.Messages.ToString());
                Logger.LogInformation($"Successfully received message: trackingid={trackingId}, batchId={batchId}, sequenceNumber={sequenceNumber}");

                byte[] messageBytes = message.GetBytes();
                var messageCopy = new Message(messageBytes);
                messageProperties.ForEach(kvp => messageCopy.Properties.Add(kvp));
                await moduleClient.SendEventAsync(Settings.Current.OutputName, messageCopy);

                // Report sending message successfully to Test Result Coordinator
                await ModuleUtil.ReportStatus(
                    trcClient,
                    Logger,
                    Settings.Current.ModuleId + ".send",
                    ModuleUtil.FormatMessagesTestResultValue(trackingId, batchId, sequenceNumber),
                    TestOperationResultType.Messages.ToString());
                Logger.LogInformation($"Successfully sent message: trackingid={trackingId}, batchId={batchId}, sequenceNumber={sequenceNumber}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in ProcessAndSendMessageAsync");
            }

            return MessageResponse.Completed;
        }
    }
}
