// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Message = Microsoft.Azure.Devices.Client.Message;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("LoadGen");

        static long messageIdCounter = 0;
        static long twinUpdateId = 0;
        static readonly Guid batchId = Guid.NewGuid();
        static readonly string twinUpdateIdLabel = "propertyUpdateId";
        static readonly RegistryManager registryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);
        static readonly AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };

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

                using (var timers = new Timers())
                {
                    Logger.LogInformation($"Batch Id={batchId}");

                    // setup the message timer
                    timers.Add(
                        Settings.Current.MessageFrequency,
                        Settings.Current.JitterFactor,
                        () => GenerateMessageAsync(moduleClient, batchId.ToString()));
                    timers.Start();
                    Logger.LogInformation("Load gen starting message send.");

                    // setup the twin update timer
                    Twin initialTwin = await GetInitialTwin(moduleClient, batchId.ToString());
                    timers.Add(
                        Settings.Current.TwinUpdateFrequency,
                        Settings.Current.JitterFactor,
                        () => TwinUpdateAsync(moduleClient, initialTwin));
                    timers.Start();
                    Logger.LogInformation("Load gen starting twin tests.");

                    (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

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

        static async Task<Twin> GetInitialTwin(ModuleClient moduleClient, string batchId)
        {
            while (true)
            {
                try{
                    return await moduleClient.GetTwinAsync();
                }
                catch (Exception e)
                {
                    string status = "[TwinUpdateAsync] Failed initial call to get twin";
                    Logger.LogError(status + $" {twinUpdateIdLabel}: {twinUpdateId}, BatchId: {batchId};{Environment.NewLine}{e}");
                    CallAnalyzerToReportStatus($"Unknown loadgen with batch id {batchId}", status, string.Empty);
                    await Task.Delay(5000);
                }
            }
        }

        static void HandleTwinMethodFailure(string moduleId, string status, string errorContext, Exception e)
        {
            Logger.LogError(status + errorContext + $"{e}");
            CallAnalyzerToReportStatus(moduleId, status, string.Empty);
        }
        static void HandleWrongPropertyFailure(string moduleId, string status, string errorContext, Twin twin)
        {
            Logger.LogError(status + errorContext);
            CallAnalyzerToReportStatus(moduleId, status, twin.ToJson());
        }

        static async Task TwinUpdateAsync(ModuleClient moduleClient, Twin initialTwin)
        {
            twinUpdateId += 1;
            string operationErrorContext = $" {twinUpdateIdLabel}: {twinUpdateId}, BatchId: {batchId};{Environment.NewLine}";

            try
            {
                string patch = String.Format("{{ properties: {{ desired: {{ {0}: {1}}} }} }}", twinUpdateIdLabel, twinUpdateId);
                await registryManager.UpdateTwinAsync(initialTwin.DeviceId, patch, initialTwin.ETag);
            }
            catch (Exception e)
            {
                string status = $"[TwinUpdateAsync] Failed call to update desired properties";
                HandleTwinMethodFailure(initialTwin.ModuleId, status, operationErrorContext, e);
                return;
            }

            var twin = new TwinCollection();
            twin[twinUpdateIdLabel] = twinUpdateId;
            try
            {
                await moduleClient.UpdateReportedPropertiesAsync(twin);
            }
            catch (Exception e)
            {
                string status = $"[TwinUpdateAsync] Failed call to update reported properties";
                HandleTwinMethodFailure(initialTwin.ModuleId, status, operationErrorContext, e);
                return;
            }

            Twin receivedTwin;
            try {
                receivedTwin = await moduleClient.GetTwinAsync();
            } 
            catch (Exception e)
            {
                string status = "[TwinUpdateAsync] Failed call to get twin";
                HandleTwinMethodFailure(initialTwin.ModuleId, status, operationErrorContext, e);
                return;
            }

            long receivedReportedPropertyId = receivedTwin.Properties.Reported[twinUpdateIdLabel];
            long receivedDesiredPropertyId = receivedTwin.Properties.Desired[twinUpdateIdLabel];
            string propertyErrorContext = $" {Environment.NewLine}Expected: {twinUpdateId}, Received: {receivedReportedPropertyId}, BatchId: {batchId};";
            if (receivedTwin.Tags[twinUpdateIdLabel] != twinUpdateId)
            {
                string status = $"[TwinUpdateAsync] Reported property update not reflected in twin";
                HandleWrongPropertyFailure(initialTwin.ModuleId, status, propertyErrorContext, receivedTwin);
                return;
            }
            if (receivedTwin.Tags[twinUpdateIdLabel] != twinUpdateId)
            {
                string status = $"[TwinUpdateAsync] Desired property update not reflected in twin";
                HandleWrongPropertyFailure(initialTwin.ModuleId, status, propertyErrorContext, receivedTwin);
                return;
            }

            CallAnalyzerToReportStatus(initialTwin.ModuleId, "[TwinUpdateAsync] Success", receivedTwin.ToJson());
        }

        static void CallAnalyzerToReportStatus(string moduleId, string status, string responseJson)
        {
            try
            {
                analyzerClient.AddResponseStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
        }
    }
}
