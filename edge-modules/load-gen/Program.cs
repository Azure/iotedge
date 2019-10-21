// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("LoadGen");

        static long messageIdCounter = 0;
        static long reportedPropertyUpdateId = 0;
        static readonly string reportedPropertyUpdateIdLabel = "reportedPropertyUpdateId";

        static readonly string moduleId = "loadGen"; // TODO: find way to get at runtime or make a field in Settings (there are multiple loadgens running)

        static async Task Main()
        {
            Logger.LogInformation($"Starting load gen with the following settings:\r\n{Settings.Current}");

            try
            {
                ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };

                using (var timers = new Timers())
                {
                    Guid batchId = Guid.NewGuid();
                    Logger.LogInformation($"Batch Id={batchId}");

                    // setup the message timer
                    timers.Add(
                        Settings.Current.MessageFrequency,
                        Settings.Current.JitterFactor,
                        () => GenerateMessageAsync(moduleClient, batchId.ToString()));

                    // setup the twin update timer
                    timers.Add(
                        Settings.Current.TwinUpdateFrequency,
                        Settings.Current.JitterFactor,
                        () => TwinUpdateAsync(moduleClient, analyzerClient, batchId.ToString()));

                    timers.Start();
                    (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);
                    Logger.LogInformation("Load gen running.");

                    await cts.Token.WhenCanceled();
                    Logger.LogInformation("Stopping timers.");
                    timers.Stop();
                    Logger.LogInformation("Closing connection to Edge Hub.");
                    await moduleClient.CloseAsync();

                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    Logger.LogInformation("Load Gen complete. Exiting.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred during load gen.\r\n{ex}");
            }
        }

        static async Task GenerateMessageAsync(ModuleClient client, string batchId)
        {
            var random = new Random();
            var bufferPool = new BufferPool();

            try
            {
                using (Buffer data = bufferPool.AllocBuffer(Settings.Current.MessageSizeInBytes))
                {
                    // generate some bytes
                    random.NextBytes(data.Data);

                    // build message
                    var messageBody = new { data = data.Data };
                    var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                    messageIdCounter += 1;
                    message.Properties.Add("sequenceNumber", messageIdCounter.ToString());
                    message.Properties.Add("batchId", batchId.ToString());

                    await client.SendEventAsync(Settings.Current.OutputName, message);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[GenerateMessageAsync] Sequence number {messageIdCounter}, BatchId: {batchId.ToString()};{Environment.NewLine}{e}");
            }
        }

        // TODO: are these status messages OK?
        static async Task TwinUpdateAsync(ModuleClient client, AnalyzerClient analyzerClient, string batchId)
        {
            reportedPropertyUpdateId += 1;
            string status;

            var twin = new TwinCollection();
            twin[reportedPropertyUpdateIdLabel] = reportedPropertyUpdateId;
            try
            {
                await client.UpdateReportedPropertiesAsync(twin);
            }
            catch (Exception e)
            {
                status = $"[TwinUpdateAsync] Failed call to update reported properties {reportedPropertyUpdateIdLabel}: {reportedPropertyUpdateId}, BatchId: {batchId};{Environment.NewLine}{e}";
                Logger.LogError(status);
                CallAnalyzerToReportStatus(moduleId, status, string.Empty, analyzerClient);
                return;
            }

            Twin twinProperties;
            try {
                twinProperties = await client.GetTwinAsync();
            } 
            catch (Exception e)
            {
                status = $"[TwinUpdateAsync] Failed call to get twin {reportedPropertyUpdateIdLabel}: {reportedPropertyUpdateId}, BatchId: {batchId};{Environment.NewLine}{e}";
                Logger.LogError(status);
                CallAnalyzerToReportStatus(moduleId, status, string.Empty, analyzerClient);
                return;
            }

            long receivedReportedPropertyId = twinProperties.Tags[reportedPropertyUpdateIdLabel];
            if (twinProperties.Tags[reportedPropertyUpdateIdLabel] != reportedPropertyUpdateId)
            {
                status = $"[TwinUpdateAsync] Reported property update not reflected in twin{Environment.NewLine}Expected: {reportedPropertyUpdateId}, Received: {receivedReportedPropertyId}, BatchId: {batchId};";
                Logger.LogError(status);
                CallAnalyzerToReportStatus(moduleId, status, twinProperties.ToJson(), analyzerClient);
                return;
            }

            CallAnalyzerToReportStatus(moduleId, "Success", twinProperties.ToJson(), analyzerClient);
        }

        // TODO: put this func in the analyzer client so we don't have to wrap try here and in direct method cloud sender
        static void CallAnalyzerToReportStatus(string moduleId, string status, string responseJson, AnalyzerClient analyzerClient)
        {
            try
            {
                analyzerClient.AddTwinResponseAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
        }
    }
}
