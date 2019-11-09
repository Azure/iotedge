// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class TwinOperator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TwinTester");
        private RegistryManager RegistryManager;
        private ModuleClient ModuleClient;
        private AnalyzerClient AnalyzerClient;
        private Storage Storage;
        private TwinState TwinState;

        public TwinOperator(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            this.RegistryManager = registryManager;
            this.ModuleClient = moduleClient;
            this.AnalyzerClient = analyzerClient;
            this.Storage = storage;
            this.TwinState = this.InitializeModuleTwin().Result;
            moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdateAsync, storage);
        }

        // TODO: get from storage instead
        private int GetNewPropertyCounter(TwinCollection properties)
        {
            int maxPropertyId = -1;
            foreach (dynamic twinUpdate in properties)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                maxPropertyId = Math.Max(int.Parse(pair.Key), maxPropertyId);
            }

            return maxPropertyId + 1;
        }

        private TwinCollection GetReportedPropertiesResetTwin(Twin originalTwin)
        {
            TwinCollection eraseReportedProperties = new TwinCollection();
            foreach (dynamic twinUpdate in originalTwin.Properties.Reported)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                eraseReportedProperties[pair.Key] = null; // erase twin property by assigning null
            }

            return eraseReportedProperties;
        }

        public async Task<TwinState> InitializeModuleTwin()
        {
            int waitPeriodInMs = 1000 * 5;
            while (true)
            {
                try
                {
                    int storageCount = (await this.Storage.GetAllDesiredPropertiesReceived()).Count + (await this.Storage.GetAllDesiredPropertiesUpdated()).Count + (await this.Storage.GetAllReportedPropertiesUpdated()).Count;
                    Twin twin = await this.RegistryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                    if (storageCount == 0)
                    {
                        Logger.LogInformation("No existing storage detected. Initializing new module twin for fresh run.");

                        // reset desired properties
                        Twin desiredPropertyResetTwin = await this.RegistryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, new Twin(), twin.ETag);

                        // reset reported properties
                        TwinCollection eraseReportedProperties = GetReportedPropertiesResetTwin(desiredPropertyResetTwin);
                        await this.ModuleClient.UpdateReportedPropertiesAsync(eraseReportedProperties);

                        await Task.Delay(waitPeriodInMs);
                        twin = await this.RegistryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                    }
                    else
                    {
                        Logger.LogInformation("Existing storage detected. Initializing reported / desired property update counters.");
                    }

                    Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
                    return new TwinState(GetNewPropertyCounter(twin.Properties.Desired), GetNewPropertyCounter(twin.Properties.Desired), twin.ETag, DateTime.MinValue);
                }
                catch (Exception e)
                {
                    Logger.LogInformation($"Retrying failed twin initialization: {e}");
                    await Task.Delay(waitPeriodInMs);
                }
            }
        }

        public bool IsPastFailureThreshold(DateTime twinUpdateTime)
        {
            DateTime comparisonPoint = new DateTime(Math.Max(twinUpdateTime.Ticks, TwinState.LastTimeOffline.Ticks));
            return comparisonPoint + Settings.Current.TwinUpdateFailureThreshold > DateTime.UtcNow;
        }

        public async Task CallAnalyzerToReportStatus(string moduleId, string status, string responseJson)
        {
            try
            {
                await this.AnalyzerClient.AddTwinStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed call to report status to analyzer: {e}");
            }
        }

        public async Task ValidateDesiredPropertyUpdates()
        {
            Twin receivedTwin;
            try
            {
                receivedTwin = await this.ModuleClient.GetTwinAsync();
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to module client get twin: {e}");
                return;
            }

            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Desired;
            Dictionary<string, DateTime> desiredPropertiesUpdated = await this.Storage.GetAllDesiredPropertiesUpdated();
            Dictionary<string, DateTime> desiredPropertiesReceived = await this.Storage.GetAllDesiredPropertiesReceived();
            Dictionary<string, string> propertiesToRemoveFromTwin = new Dictionary<string, string>();
            foreach (KeyValuePair<string, DateTime> desiredPropertyUpdate in desiredPropertiesUpdated)
            {
                bool doesTwinHaveUpdate = propertyUpdatesFromTwin.Contains(desiredPropertyUpdate.Key);
                bool hasModuleReceivedCallback = desiredPropertiesReceived.ContainsKey(desiredPropertyUpdate.Key);
                if (doesTwinHaveUpdate && hasModuleReceivedCallback)
                {
                    string successStatus = $"{(int)StatusCode.Success}: Successfully sent/received desired property update";

                    await CallAnalyzerToReportStatus(Settings.Current.ModuleId, successStatus, string.Empty);
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
                    await CallAnalyzerToReportStatus(Settings.Current.ModuleId, failureStatus, string.Empty);

                    propertiesToRemoveFromTwin.Add(desiredPropertyUpdate.Key, null); // erase twin property by assigning null
                }

                try
                {
                    await this.Storage.RemoveDesiredPropertyUpdate(desiredPropertyUpdate.Key);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to remove validated reported property id {desiredPropertyUpdate.Key} from storage: {e}");
                }
            }

            try
            {
                string patch = $"{{ properties: {{ desired: {JsonConvert.SerializeObject(propertiesToRemoveFromTwin)} }}";
                Twin newTwin = await this.RegistryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, TwinState.TwinETag);
                TwinState.TwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to remove successful desired property updates: {e}");
            }
        }

        public async Task ValidateReportedPropertyUpdates()
        {
            Twin receivedTwin;
            try
            {
                receivedTwin = await this.RegistryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                TwinState.TwinETag = receivedTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to registry manager get twin: {e}");
                TwinState.LastTimeOffline = DateTime.UtcNow;
                return;
            }

            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Reported;
            Dictionary<string, DateTime> reportedPropertiesUpdated = await this.Storage.GetAllReportedPropertiesUpdated();
            foreach (KeyValuePair<string, DateTime> reportedPropertyUpdate in reportedPropertiesUpdated)
            {
                if (propertyUpdatesFromTwin.Contains(reportedPropertyUpdate.Key))
                {
                    try
                    {
                        await this.Storage.RemoveReportedPropertyUpdate(reportedPropertyUpdate.Key);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to remove validated reported property id {reportedPropertyUpdate.Key} from storage: {e}");
                        continue;
                    }

                    string successStatus = $"{(int)StatusCode.Success}: Successfully sent/received reported property update";
                    Logger.LogInformation(successStatus + $" {reportedPropertyUpdate.Key}");
                    await CallAnalyzerToReportStatus(Settings.Current.ModuleId, successStatus, string.Empty);
                }
                else if (IsPastFailureThreshold(reportedPropertyUpdate.Value))
                {
                    string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwin}: Failure receiving reported property update";
                    Logger.LogError(failureStatus + $"for reported property update {reportedPropertyUpdate.Key}");
                    await CallAnalyzerToReportStatus(Settings.Current.ModuleId, failureStatus, string.Empty);
                }
            }

            TwinCollection eraseReportedProperties = GetReportedPropertiesResetTwin(receivedTwin);
            try
            {
                await this.ModuleClient.UpdateReportedPropertiesAsync(eraseReportedProperties);
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to post-update twin reset: {e}");
            }
        }

        public async Task PerformDesiredPropertyUpdate()
        {
            try
            {
                string desiredPropertyUpdate = new string('1', Settings.Current.TwinUpdateCharCount); // dummy twin update needs to be any number
                string patch = string.Format("{{ properties: {{ desired: {{ {0}: {1}}} }} }}", TwinState.DesiredPropertyUpdateCounter, desiredPropertyUpdate);
                Twin newTwin = await this.RegistryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, TwinState.TwinETag);
                TwinState.TwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to desired property update: {e}");
                return;
            }

            try
            {
                await this.Storage.AddDesiredPropertyUpdate(TwinState.DesiredPropertyUpdateCounter.ToString());
                TwinState.DesiredPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed adding desired property update to storage: {e}");
            }
        }

        public async Task PerformReportedPropertyUpdate()
        {
            string reportedPropertyUpdate = new string('1', Settings.Current.TwinUpdateCharCount); // dummy twin update needs to be any number
            var twin = new TwinCollection();
            twin[TwinState.ReportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdate;
            try
            {
                await this.ModuleClient.UpdateReportedPropertiesAsync(twin);
            }
            catch (Exception e)
            {
                string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateCallFailure}: Failed call to update reported properties";
                Logger.LogError(failureStatus + $": {e}");
                await CallAnalyzerToReportStatus(Settings.Current.ModuleId, failureStatus, string.Empty);
                return;
            }

            try
            {
                await this.Storage.AddReportedPropertyUpdate(TwinState.ReportedPropertyUpdateCounter.ToString());
                TwinState.ReportedPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed adding reported property update to storage: {e}");
                return;
            }
        }

        private async Task OnDesiredPropertyUpdateAsync(TwinCollection desiredProperties, object userContext)
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
