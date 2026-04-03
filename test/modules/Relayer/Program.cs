// Copyright (c) Microsoft. All rights reserved.
namespace Relayer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /*
     * Module for relaying messages. It receives a message and passes it on.
     */
    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("Relayer");
        static volatile bool isFinished = false;
        static ConcurrentBag<string> resultsReceived = new ConcurrentBag<string>();

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting Relayer with the following settings: \r\n{Settings.Current}");
            IotHubModuleClient moduleClient = null;

            try
            {
                moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    null,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);
                DuplicateMessageAuditor duplicateMessageAuditor = new DuplicateMessageAuditor(Settings.Current.MessageDuplicateTolerance);
                MessageHandlerContext messageHandlerContext = new MessageHandlerContext(moduleClient, duplicateMessageAuditor);

                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                await SetIsFinishedDirectMethodAsync(moduleClient);

                // Receive a message and call ProcessAndSendMessageAsync to send it on its way
                await moduleClient.SetIncomingMessageCallbackAsync(async (IncomingMessage message) =>
                {
                    if (message.InputName == Settings.Current.InputName)
                    {
                        return await ProcessAndSendMessageAsync(message, messageHandlerContext);
                    }
                    return MessageAcknowledgement.Complete;
                });

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
                await (moduleClient?.DisposeAsync() ?? default);
            }
        }

        static async Task<MessageAcknowledgement> ProcessAndSendMessageAsync(IncomingMessage message, object userContext)
        {
            // TODO: v2 SDK - IncomingMessage.SystemProperties is not accessible. Connection device/module IDs not directly available.
            Logger.LogInformation($"Received message from device: unknown, module: unknown");

            var testResultCoordinatorUrl = Option.None<Uri>();

            if (Settings.Current.EnableTrcReporting)
            {
                var builder = new UriBuilder(Settings.Current.TestResultCoordinatorUrl);
                builder.Host = Dns.GetHostEntry(Settings.Current.TestResultCoordinatorUrl.Host).AddressList[0].ToString();
                testResultCoordinatorUrl = Option.Some(builder.Uri);
            }

            try
            {
                if (!(userContext is MessageHandlerContext messageHandlerContext))
                {
                    throw new InvalidOperationException("UserContext doesn't contain expected value");
                }

                IotHubModuleClient moduleClient = messageHandlerContext.ModuleClient;
                DuplicateMessageAuditor duplicateMessageAuditor = messageHandlerContext.DuplicateMessageAuditor;

                // Must make a new message instead of reusing the old message because of the way the SDK sends messages
                string trackingId = string.Empty;
                string batchId = string.Empty;
                string sequenceNumber = string.Empty;
                var messageProperties = new List<KeyValuePair<string, string>>();
                var testResultReportingClient = Option.None<TestResultReportingClient>();

                if (Settings.Current.EnableTrcReporting)
                {
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
                        return MessageAcknowledgement.Complete;
                    }

                    if (duplicateMessageAuditor.ShouldFilterMessage(sequenceNumber))
                    {
                        return MessageAcknowledgement.Complete;
                    }

                    // Report receiving message successfully to Test Result Coordinator
                    testResultReportingClient = Option.Some(new TestResultReportingClient { BaseUrl = testResultCoordinatorUrl.OrDefault().AbsoluteUri });
                    var testResultReceived = new MessageTestResult(Settings.Current.ModuleId + ".receive", DateTime.UtcNow)
                    {
                        TrackingId = trackingId,
                        BatchId = batchId,
                        SequenceNumber = sequenceNumber
                    };

                    await ModuleUtil.ReportTestResultAsync(testResultReportingClient.OrDefault(), Logger, testResultReceived);

                    Logger.LogInformation($"Successfully received message: trackingid={trackingId}, batchId={batchId}, sequenceNumber={sequenceNumber}");
                }

                if (!Settings.Current.ReceiveOnly)
                {
                    byte[] messageBytes = message.Payload;
                    var messageCopy = new TelemetryMessage(messageBytes);
                    messageProperties.ForEach(kvp => messageCopy.Properties.Add(kvp));
                    await moduleClient.SendMessageToRouteAsync(Settings.Current.OutputName, messageCopy);
                    // TODO: v2 SDK - IncomingMessage.SystemProperties is not accessible. Connection device/module IDs not directly available.
                    Logger.LogInformation($"Message relayed upstream for device: unknown, module: unknown");

                    if (Settings.Current.EnableTrcReporting)
                    {
                        // Report sending message successfully to Test Result Coordinator
                        var testResultSent = new MessageTestResult(Settings.Current.ModuleId + ".send", DateTime.UtcNow)
                        {
                            TrackingId = trackingId,
                            BatchId = batchId,
                            SequenceNumber = sequenceNumber
                        };

                        await ModuleUtil.ReportTestResultAsync(testResultReportingClient.OrDefault(), Logger, testResultSent);
                        Logger.LogInformation($"Successfully reported message: trackingid={trackingId}, batchId={batchId}, sequenceNumber={sequenceNumber}");
                    }
                }
                else
                {
                    int uniqueResultsExpected = Settings.Current.UniqueResultsExpected.Expect<ArgumentException>(() => throw new ArgumentException("Must supply this value if in ReceiveOnly mode"));
                    if (!resultsReceived.Contains(sequenceNumber))
                    {
                        resultsReceived.Add(sequenceNumber);
                    }

                    if (resultsReceived.Count == uniqueResultsExpected)
                    {
                        isFinished = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error in {nameof(ProcessAndSendMessageAsync)} method");
            }

            return MessageAcknowledgement.Complete;
        }

        private static async Task SetIsFinishedDirectMethodAsync(IotHubModuleClient client)
        {
            await client.SetDirectMethodCallbackAsync(async (DirectMethodRequest methodRequest) =>
            {
                if (methodRequest.MethodName == "IsFinished")
                {
                    return IsFinished();
                }
                return new DirectMethodResponse((int)HttpStatusCode.NotFound);
            });
        }

        private static DirectMethodResponse IsFinished()
        {
            string response = JsonConvert.SerializeObject(new PriorityQueueTestStatus(isFinished, resultsReceived.Count));
            return new DirectMethodResponse((int)HttpStatusCode.OK) { Payload = Encoding.UTF8.GetBytes(response) };
        }
    }
}
