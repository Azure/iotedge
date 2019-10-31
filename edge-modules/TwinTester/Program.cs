// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
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

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TwinTester");
        static readonly Guid BatchId = Guid.NewGuid();
        static readonly RegistryManager RegistryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);
        static readonly AnalyzerClient AnalyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };
        static readonly string DeviceId = Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID");
        static readonly string ModuleId = Environment.GetEnvironmentVariable("IOTEDGE_MODULEID");
        static string currentTwinETag = string.Empty;
        static long desiredPropertyUpdateCounter = 0;
        static long reportedPropertyUpdateCounter = 0;

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

                moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdateAsync);

                using (var timers = new Timers())
                {
                    Logger.LogInformation($"Batch Id={BatchId}");

                    // setup the twin update timer
                    await InitializeETag(BatchId.ToString());
                    timers.Add(
                        Settings.Current.TwinUpdateFrequency,
                        Settings.Current.JitterFactor,
                        () => PerformTwinTestsAsync(moduleClient));
                    timers.Start();
                    Logger.LogInformation("TwinTester starting twin tests.");

                    (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                    await cts.Token.WhenCanceled();
                    Logger.LogInformation("Stopping timers.");
                    timers.Stop();
                    Logger.LogInformation("Closing connection to Edge Hub.");
                    await moduleClient.CloseAsync();

                    completed.Set();
                    handler.ForEach(h => GC.KeepAlive(h));
                    Logger.LogInformation("Twin tests complete. Exiting.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error occurred during twin test setup.\r\n{ex}");
            }
        }

        // TODO: make void
        static async Task InitializeETag(string batchId)
        {
            while (true)
            {
                try
                {
                    Twin twin = await RegistryManager.GetTwinAsync(DeviceId, ModuleId);
                    Logger.LogDebug("initial twin: {0}", twin.ToJson());
                    currentTwinETag = twin.ETag;
                }
                catch (Exception e)
                {
                    Logger.LogInformation($"Failed initial call to get twin: {e}");
                    await Task.Delay(5000); // TODO: tune wait period
                }
            }
        }

        static async Task OnDesiredPropertyUpdateAsync(TwinCollection desiredProperties, object userContext)
        {
            // iterate through fields in twin collection

            // store received desired properties 
        }

        static bool isTwinValid(Twin receivedTwin)
        {
            // crosscheck reported properties made with reported properties in twin (give failure threshold)

            // crosscheck desired properties made, desired properties received, and desired properties in twin
        }

        // TODO: document somewhere that this method could fail due to too short interval of twin updates not giving edgehub enough time
        static async Task PerformTwinTestsAsync(ModuleClient moduleClient)
        {
            // attempt to get twin, verify with failure threshold, remove from storage, and send report
            Twin receivedTwin;
            try
            {
                receivedTwin = await RegistryManager.GetTwinAsync(DeviceId, ModuleId);
                isTwinValid(receivedTwin);
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to get twin: {e}");
            }

            // perform desired property update and store
            try
            {
                string patch = string.Format("{{ properties: {{ desired: {{ {0}: {0}}} }} }}", desiredPropertyUpdateCounter);
                Twin newTwin = await RegistryManager.UpdateTwinAsync(DeviceId, ModuleId, patch, currentTwinETag);
                currentTwinETag = newTwin.ETag;
                StoreDesiredPropertyUpdate();
                desiredPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to desired property update: {e}");
            }


            // perform reported property update and store
            var twin = new TwinCollection();
            twin[reportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdateCounter;
            try
            {
                await moduleClient.UpdateReportedPropertiesAsync(twin);
                StoreReportedPropertyUpdate()
            }
            catch (Exception e)
            {
                string failureStatus = "Failed call to update reported properties";
                Logger.LogError(failureStatus + $": {e}");
                CallAnalyzerToReportStatus(ModuleId, failureStatus, string.Empty);
            }
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
