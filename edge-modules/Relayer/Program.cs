// Copyright (c) Microsoft. All rights reserved.
namespace Relayer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Client;

    using TestOperationResult = TestResultCoordinator.Client.TestOperationResult;
    /*
     * Module for relaying messages. It receives a message and passes it on.
     */
    class Program
    {
        const string ModuleId = "Relayer";
        static readonly ILogger Logger = ModuleUtil.CreateLogger(ModuleId);

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting Relayer with the following settings: \r\n{Settings.Current}");

            try
            {
                ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
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
                byte[] messageBytes = message.GetBytes();
                var messageCopy = new Message(messageBytes);
                foreach (KeyValuePair<string, string> prop in message.Properties)
                {
                    messageCopy.Properties.Add(prop.Key, prop.Value);
                }

                TestResultCoordinatorClient trcClient = new TestResultCoordinatorClient { BaseUrl = testResultCoordinatorUrl.AbsoluteUri };
                await ReportStatus(trcClient, ModuleId, messageCopy.ToString());

                await moduleClient.SendEventAsync(Settings.Current.OutputName, messageCopy);
                Logger.LogInformation("Successfully sent a message");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in ProcessAndSendMessageAsync");
            }

            return MessageResponse.Completed;
        }

        static async Task ReportStatus(TestResultCoordinatorClient trcClient, string moduleId, string message)
        {
            try
            {
                await trcClient.ReportResultAsync(new TestOperationResult { Source = moduleId, Result = message, CreatedAt = DateTime.UtcNow, Type = moduleId });
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to report status to TestResultCoordinator");
            }
        }
    }
}
