// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
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
        static string currentTwinETag = string.Empty;
        static long desiredPropertyUpdateCounter = 0;
        static long reportedPropertyUpdateCounter = 0;

        static async Task Main()
        {
            Logger.LogInformation($"Starting load gen with the following settings:\r\n{Settings.Current}");

            try
            {
                Storage storage = new Storage();
                await storage.Init(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.StorageOptimizeForPerformance);

                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);
                
                ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);
                await moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdateAsync, storage);

                AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };

                using (var timers = new Timers())
                {
                    // setup the twin update timer
                    currentTwinETag = await GetTwinETag(registryManager);
                    timers.Add(
                        Settings.Current.TwinUpdateFrequency,
                        Settings.Current.JitterFactor,
                        () => PerformTwinTestsAsync(registryManager, moduleClient, analyzerClient, storage));
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

        static async Task<string> GetTwinETag(RegistryManager registryManager)
        {
            while (true)
            {
                try
                {
                    Twin twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                    Logger.LogDebug("initial twin: {0}", twin.ToJson());
                    return twin.ETag;
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

            Logger.LogDebug("STARTED ON DESIRED PROPERTY CALLBACK");
            Logger.LogDebug(desiredProperties.ToJson());
            foreach(string key in desiredProperties)
            {
                Logger.LogDebug(key);
            }
        }

        static async Task ValidateDesiredPropertyUpdates(ModuleClient moduleClient, AnalyzerClient analyzerClient)
        {
            // get moduleClientTwin
            // pull all desired properties from twin into set
            // iterate through known desired property updates (storage) 
            //      if in twin and in callback storage:
            //          remove from twin
            //          remove from both storages
            //      else if (in twin but not in callback storage):
            //          check failure threshold or report error?
            //      else if (not in twin and in callback storage)
            //          report failure
            //      else (not in twin and not in callback storage)
            //          check failure threshold
            // 
            // clear twin properties that are not in desired property update storage (could be populated from updates that then failed to write to storage) ????????
        }

        static async Task ValidateReportedPropertyUpdates(RegistryManager registryManager, AnalyzerClient analyzerClient)
        {
            // get registry client twin
            // pull all reported properties from twin into set
            // iterate through known reported property updates (storage) 
            //      if in twin:
            //          remove from twin
            //          remove from storage
            //      else
            //          check failure threshold for missing property
            // 
            // clear twin entirely (could be populated from updates that then failed to write to storage)

            Twin receivedTwin;
            try
            {
                receivedTwin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to registry manager get twin: {e}");
            }
        }

        static async Task PerformDesiredPropertyUpdate(RegistryManager registryManager, Storage storage)
        {
            try
            {
                // TODO: add timestamp
                string patch = string.Format("{{ properties: {{ desired: {{ {0}: {0}}} }} }}", desiredPropertyUpdateCounter);
                Twin newTwin = await registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, currentTwinETag);
                currentTwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to desired property update: {e}");
            }

            try
            {
                await storage.AddDesiredPropertyUpdate(new KeyValuePair<string, DateTime>(desiredPropertyUpdateCounter.ToString(), DateTime.Now));
                desiredPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed adding desired property update to storage: {e}");
            }
        }

        static async Task PerformReportedPropertyUpdate(ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            var twin = new TwinCollection();
            twin[reportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdateCounter;
            try
            {
                await moduleClient.UpdateReportedPropertiesAsync(twin);
            }
            catch (Exception e)
            {
                string failureStatus = "Failed call to update reported properties";
                Logger.LogError(failureStatus + $": {e}");
                await CallAnalyzerToReportStatus(analyzerClient, Settings.Current.ModuleId, failureStatus, string.Empty);
                return;
            }

            try
            {
                await storage.AddReportedPropertyUpdate(new KeyValuePair<string, DateTime>(reportedPropertyUpdateCounter.ToString(), DateTime.Now));
                reportedPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed adding reported property update to storage: {e}");
                return;
            }

            return;
        }

        // TODO: document somewhere that this method could fail due to too short interval of twin updates not giving edgehub enough time
        static async Task PerformTwinTestsAsync(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            await ValidateDesiredPropertyUpdates(moduleClient, analyzerClient);
            // await ValidateReportedPropertyUpdates(registryManager, analyzerClient);
            await PerformDesiredPropertyUpdate(registryManager, storage);
            // await PerformReportedPropertyUpdate(moduleClient, analyzerClient, storage);
        }

        static async Task CallAnalyzerToReportStatus(AnalyzerClient analyzerClient, string moduleId, string status, string responseJson)
        {
            try
            {
                await analyzerClient.AddResponseStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed call to report status to analyzer: {e}");
            }
        }
    }
}
