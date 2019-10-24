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
        static readonly Guid BatchId = Guid.NewGuid();
        static readonly RegistryManager RegistryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);
        static readonly AnalyzerClient AnalyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };
        static readonly string DeviceId = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
        static readonly string ModuleId = Environment.GetEnvironmentVariable("IOTEDGE_MODULEID");
        static readonly string TwinUpdateIdLabel = "propertyUpdateId";
        static long MessageId = 0;
        static long TwinUpdateId = 0;

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
                    Logger.LogInformation($"Batch Id={BatchId}");

                    // setup the message timer
                    timers.Add(
                        Settings.Current.MessageFrequency,
                        Settings.Current.JitterFactor,
                        () => GenerateMessageAsync(moduleClient, BatchId.ToString()));
                    timers.Start();
                    Logger.LogInformation("Load gen starting message send.");

                    // setup the twin update timer
                    Twin initialTwin = await GetInitialTwin(moduleClient, BatchId.ToString());
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
                Logger.LogError($"Error occurred during load gen setup.\r\n{ex}");
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
                    MessageId += 1;
                    message.Properties.Add("sequenceNumber", MessageId.ToString());
                    message.Properties.Add("batchId", batchId.ToString());

                    await client.SendEventAsync(Settings.Current.OutputName, message);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[GenerateMessageAsync] Sequence number {MessageId}, BatchId: {batchId.ToString()};{Environment.NewLine}{e}");
            }
        }

        static async Task<Twin> GetInitialTwin(ModuleClient moduleClient, string batchId)
        {
            while (true)
            {
                try
                {
                    Twin twin = await RegistryManager.GetTwinAsync(DeviceId);
                    Logger.LogDebug(twin.ToJson());
                    return twin;
                }
                catch (Exception e)
                {
                    string status = "[TwinUpdateAsync] Failed initial call to get twin";
                    Logger.LogError(status + $" {TwinUpdateIdLabel}: {TwinUpdateId}, BatchId: {batchId};{Environment.NewLine}{e}");
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
            Logger.LogError(initialTwin.ToJson());
            Logger.LogError(initialTwin.DeviceId);

            TwinUpdateId += 1;
            string operationErrorContext = $" {TwinUpdateIdLabel}: {TwinUpdateId}, BatchId: {BatchId};{Environment.NewLine}";

            try
            {
                string patch = string.Format("{{ properties: {{ desired: {{ {0}: {1}}} }} }}", TwinUpdateIdLabel, TwinUpdateId);
                await RegistryManager.UpdateTwinAsync(DeviceId, ModuleId, patch, initialTwin.ETag);
            }
            catch (Exception e)
            {
                string status = $"[TwinUpdateAsync] Failed call to update desired properties";
                HandleTwinMethodFailure(ModuleId, status, operationErrorContext, e);
                return;
            }

            var twin = new TwinCollection();
            twin[TwinUpdateIdLabel] = TwinUpdateId;
            try
            {
                await moduleClient.UpdateReportedPropertiesAsync(twin);
            }
            catch (Exception e)
            {
                string status = $"[TwinUpdateAsync] Failed call to update reported properties";
                HandleTwinMethodFailure(ModuleId, status, operationErrorContext, e);
                return;
            }

            Twin receivedTwin;
            try
            {
                receivedTwin = await moduleClient.GetTwinAsync();
            }
            catch (Exception e)
            {
                string status = "[TwinUpdateAsync] Failed call to get twin";
                HandleTwinMethodFailure(ModuleId, status, operationErrorContext, e);
                return;
            }

            long receivedReportedPropertyId = receivedTwin.Properties.Reported[TwinUpdateIdLabel];
            long receivedDesiredPropertyId = receivedTwin.Properties.Desired[TwinUpdateIdLabel];
            string propertyErrorContext = $" {Environment.NewLine}Expected: {TwinUpdateId}, Received: {receivedReportedPropertyId}, BatchId: {BatchId};";

            if (receivedTwin.Tags[TwinUpdateIdLabel] != TwinUpdateId)
            {
                string status = $"[TwinUpdateAsync] Reported property update not reflected in twin";
                HandleWrongPropertyFailure(ModuleId, status, propertyErrorContext, receivedTwin);
                return;
            }

            if (receivedTwin.Tags[TwinUpdateIdLabel] != TwinUpdateId)
            {
                string status = $"[TwinUpdateAsync] Desired property update not reflected in twin";
                HandleWrongPropertyFailure(ModuleId, status, propertyErrorContext, receivedTwin);
                return;
            }

            CallAnalyzerToReportStatus(ModuleId, "[TwinUpdateAsync] Success", receivedTwin.ToJson());
        }

        static void CallAnalyzerToReportStatus(string moduleId, string status, string responseJson)
        {
            try
            {
                AnalyzerClient.AddResponseStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
        }
    }
}
