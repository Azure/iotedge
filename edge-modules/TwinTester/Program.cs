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
    using Newtonsoft.Json;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TwinTester");
        static string currentTwinETag = string.Empty;
        static long desiredPropertyUpdateCounter = 0;
        static long reportedPropertyUpdateCounter = 0;

        static async Task Main()
        {
            Logger.LogInformation($"Starting twin tester with the following settings:\r\n{Settings.Current}");

            try
            {
                Storage storage = new Storage();
                storage.Init(Settings.Current.StoragePath, new SystemEnvironment(), Settings.Current.StorageOptimizeForPerformance);

                RegistryManager registryManager = RegistryManager.CreateFromConnectionString(Settings.Current.ServiceClientConnectionString);

                ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    Settings.Current.TransportType,
                    ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                    ModuleUtil.DefaultTransientRetryStrategy,
                    Logger);

                AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = Settings.Current.AnalyzerUrl };

                currentTwinETag = await InitializeModuleTwin(registryManager, moduleClient, storage);

                await moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdateAsync, storage);

                using (var timers = new Timers())
                {
                    // setup the twin update timer
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

        static async Task<string> InitializeModuleTwin(RegistryManager registryManager, ModuleClient moduleClient, Storage storage)
        {
            while (true)
            {
                try
                {
                    int storageCount = (await storage.GetAllDesiredPropertiesReceived()).Count + (await storage.GetAllDesiredPropertiesUpdated()).Count + (await storage.GetAllReportedPropertiesUpdated()).Count;
                    if (storageCount == 0)
                    {
                        Logger.LogInformation("No existing storage detected. Initializing new module twin for fresh run.");

                        // reset desired properties
                        Twin originalTwin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                        Twin desiredPropertyResetTwin = await registryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, new Twin(), originalTwin.ETag);

                        // reset reported properties
                        TwinCollection eraseReportedProperties = GetReportedPropertiesResetTwin(moduleClient, desiredPropertyResetTwin);
                        await moduleClient.UpdateReportedPropertiesAsync(eraseReportedProperties);

                        await Task.Delay(1000 * 5); // TODO: tune delay
                    }

                    Twin initializedTwin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);

                    Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(initializedTwin, Formatting.Indented)}");

                    return initializedTwin.ETag;
                }
                catch (Exception e)
                {
                    Logger.LogInformation($"Retrying failed twin initialization: {e}");
                    await Task.Delay(5000); // TODO: tune wait period
                }
            }
        }

        static TwinCollection GetReportedPropertiesResetTwin(ModuleClient moduleClient, Twin originalTwin)
        {
            TwinCollection eraseReportedProperties = new TwinCollection();
            foreach (dynamic twinUpdate in originalTwin.Properties.Reported)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                eraseReportedProperties[pair.Key] = null; // erase twin property by assigning null
            }

            return eraseReportedProperties;
        }

        static bool IsPastFailureThreshold(DateTime twinUpdateTime)
        {
            return twinUpdateTime + Settings.Current.TwinUpdateFailureThreshold > DateTime.UtcNow;
        }

        static async Task CallAnalyzerToReportStatus(AnalyzerClient analyzerClient, string moduleId, string status, string responseJson)
        {
            try
            {
                await analyzerClient.AddTwinStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed call to report status to analyzer: {e}");
            }
        }

        static async Task ValidateDesiredPropertyUpdates(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            Twin receivedTwin;
            try
            {
                receivedTwin = await moduleClient.GetTwinAsync();
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to module client get twin: {e}");
                return;
            }

            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Desired;
            Dictionary<string, DateTime> desiredPropertiesUpdated = await storage.GetAllDesiredPropertiesUpdated();
            Dictionary<string, DateTime> desiredPropertiesReceived = await storage.GetAllDesiredPropertiesReceived();
            Dictionary<string, string> propertiesToRemoveFromTwin = new Dictionary<string, string>();
            foreach (KeyValuePair<string, DateTime> desiredPropertyUpdate in desiredPropertiesUpdated)
            {
                bool doesTwinHaveUpdate = propertyUpdatesFromTwin.Contains(desiredPropertyUpdate.Key);
                bool hasModuleReceivedCallback = desiredPropertiesReceived.ContainsKey(desiredPropertyUpdate.Key);
                if (doesTwinHaveUpdate && hasModuleReceivedCallback)
                {
                    try
                    {
                        await storage.RemoveDesiredPropertyUpdate(desiredPropertyUpdate.Key);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to remove validated reported property id {desiredPropertyUpdate.Key} from storage: {e}");
                        continue;
                    }

                    string successStatus = $"{(int)StatusCode.Success}: Successfully sent/received desired property update";

                    await CallAnalyzerToReportStatus(analyzerClient, Settings.Current.ModuleId, successStatus, string.Empty);
                    Logger.LogInformation(successStatus + $" {desiredPropertyUpdate.Key}");

                    propertiesToRemoveFromTwin.Add(desiredPropertyUpdate.Key, null); // erase twin property by assigning null
                }
                else if (IsPastFailureThreshold(desiredPropertyUpdate.Value))
                {
                    string failureStatus;
                    if (doesTwinHaveUpdate && !hasModuleReceivedCallback)
                    {
                        failureStatus = $"{(int)StatusCode.DesiredPropertyUpdateNoCallbackReceived}: Failure receiving desired property update in callback";
                    }
                    else if (!doesTwinHaveUpdate && hasModuleReceivedCallback)
                    {
                        failureStatus = $"{(int)StatusCode.DesiredPropertyUpdateNotInEdgeTwin}: Failure receiving desired property update in twin";
                    }
                    else
                    {
                        failureStatus = $"{(int)StatusCode.DesiredPropertyUpdateTotalFailure}: Failure receiving desired property update in both twin and callback";
                    }

                    Logger.LogError(failureStatus + $" for update #{desiredPropertyUpdate.Key}");
                    await CallAnalyzerToReportStatus(analyzerClient, Settings.Current.ModuleId, failureStatus, string.Empty);

                    propertiesToRemoveFromTwin.Add(desiredPropertyUpdate.Key, null); // erase twin property by assigning null
                }
            }

            try
            {
                string patch = $"{{ properties: {{ desired: {JsonConvert.SerializeObject(propertiesToRemoveFromTwin)} }}";
                Twin newTwin = await registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, currentTwinETag);
                currentTwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to remove successful desired property updates: {e}");
            }
        }

        static async Task ValidateReportedPropertyUpdates(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            Twin receivedTwin;
            try
            {
                receivedTwin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to registry manager get twin: {e}");
                return;
            }

            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Reported;
            Dictionary<string, DateTime> reportedPropertiesUpdated = await storage.GetAllReportedPropertiesUpdated();
            foreach (KeyValuePair<string, DateTime> reportedPropertyUpdate in reportedPropertiesUpdated)
            {
                if (propertyUpdatesFromTwin.Contains(reportedPropertyUpdate.Key))
                {
                    try
                    {
                        await storage.RemoveReportedPropertyUpdate(reportedPropertyUpdate.Key);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to remove validated reported property id {reportedPropertyUpdate.Key} from storage: {e}");
                        continue;
                    }

                    string successStatus = $"{(int)StatusCode.Success}: Successfully sent/received reported property update";
                    Logger.LogInformation(successStatus + $" {reportedPropertyUpdate.Key}");
                    await CallAnalyzerToReportStatus(analyzerClient, Settings.Current.ModuleId, successStatus, string.Empty);
                }
                else if (IsPastFailureThreshold(reportedPropertyUpdate.Value))
                {
                    string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwin}: Failure receiving reported property update";
                    Logger.LogError(failureStatus + $"for reported property update {reportedPropertyUpdate.Key}");
                    await CallAnalyzerToReportStatus(analyzerClient, Settings.Current.ModuleId, failureStatus, string.Empty);
                }
            }

            TwinCollection eraseReportedProperties = GetReportedPropertiesResetTwin(moduleClient, receivedTwin);
            try
            {
                await moduleClient.UpdateReportedPropertiesAsync(eraseReportedProperties);
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to post-update twin reset: {e}");
            }
        }

        static async Task PerformDesiredPropertyUpdate(RegistryManager registryManager, Storage storage)
        {
            try
            {
                string desiredPropertyUpdate = new string('a', Settings.Current.TwinUpdateCharCount);
                string patch = string.Format("{{ properties: {{ desired: {{ {0}: {1}}} }} }}", desiredPropertyUpdateCounter, desiredPropertyUpdate);
                Twin newTwin = await registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, currentTwinETag);
                currentTwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to desired property update: {e}");
                return;
            }

            try
            {
                await storage.AddDesiredPropertyUpdate(desiredPropertyUpdateCounter.ToString());
                desiredPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed adding desired property update to storage: {e}");
            }
        }

        static async Task PerformReportedPropertyUpdate(ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            string reportedPropertyUpdate = new string('a', Settings.Current.TwinUpdateCharCount); // TODO: change string value?
            var twin = new TwinCollection();
            twin[reportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdate;
            try
            {
                await moduleClient.UpdateReportedPropertiesAsync(twin);
            }
            catch (Exception e)
            {
                string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateCallFailure}: Failed call to update reported properties";
                Logger.LogError(failureStatus + $": {e}");
                await CallAnalyzerToReportStatus(analyzerClient, Settings.Current.ModuleId, failureStatus, string.Empty);
                return;
            }

            try
            {
                await storage.AddReportedPropertyUpdate(reportedPropertyUpdateCounter.ToString());
                reportedPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed adding reported property update to storage: {e}");
                return;
            }
        }

        // TODO: document somewhere that this method could fail due to too short interval of twin updates not giving edgehub enough time
        static async Task PerformTwinTestsAsync(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            await ValidateDesiredPropertyUpdates(registryManager, moduleClient, analyzerClient, storage);
            await ValidateReportedPropertyUpdates(registryManager, moduleClient, analyzerClient, storage);
            await PerformDesiredPropertyUpdate(registryManager, storage);
            await PerformReportedPropertyUpdate(moduleClient, analyzerClient, storage);
        }

        static async Task OnDesiredPropertyUpdateAsync(TwinCollection desiredProperties, object userContext)
        {
            // TODO: If expected behavior is calling once per desired property update, then we should not be looping
            Storage storage = (Storage)userContext;
            foreach (dynamic twinUpdate in desiredProperties)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                await storage.AddDesiredPropertyReceived(pair.Key);
            }
        }
    }
}
