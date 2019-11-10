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
        private RegistryManager registryManager;
        private ModuleClient moduleClient;
        private AnalyzerClient analyzerClient;
        private Storage storage;
        private TwinState twinState;

        public TwinOperator(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, Storage storage)
        {
            this.registryManager = registryManager;
            this.moduleClient = moduleClient;
            this.analyzerClient = analyzerClient;
            this.storage = storage;
            this.twinState = this.InitializeModuleTwin().Result;
            this.moduleClient.SetDesiredPropertyUpdateCallbackAsync(this.OnDesiredPropertyUpdateAsync, storage);
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
                    TwinState initializedState;
                    Twin twin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                    int storageCount = (await this.storage.GetAllDesiredPropertiesReceived()).Count + (await this.storage.GetAllDesiredPropertiesUpdated()).Count + (await this.storage.GetAllReportedPropertiesUpdated()).Count;
                    if (storageCount == 0)
                    {
                        Logger.LogInformation("No existing storage detected. Initializing new module twin for fresh run.");

                        // reset desired properties
                        Twin desiredPropertyResetTwin = await this.registryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, new Twin(), twin.ETag);

                        // reset reported properties
                        TwinCollection eraseReportedProperties = this.GetReportedPropertiesResetTwin(desiredPropertyResetTwin);
                        await this.moduleClient.UpdateReportedPropertiesAsync(eraseReportedProperties);

                        await Task.Delay(waitPeriodInMs);
                        twin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                        initializedState = new TwinState(0, 0, twin.ETag, DateTime.MinValue);
                    }
                    else
                    {
                        Logger.LogInformation("Existing storage detected. Initializing reported / desired property update counters.");
                        initializedState = new TwinState(this.GetNewPropertyCounter(twin.Properties.Desired), this.GetNewPropertyCounter(twin.Properties.Desired), twin.ETag, DateTime.MinValue);
                    }

                    Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
                    return initializedState;
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
            DateTime comparisonPoint = new DateTime(Math.Max(twinUpdateTime.Ticks, this.twinState.LastTimeOffline.Ticks));
            return comparisonPoint + Settings.Current.TwinUpdateFailureThreshold > DateTime.UtcNow;
        }

        public async Task CallAnalyzerToReportStatus(string moduleId, string status, string responseJson)
        {
            try
            {
                await this.analyzerClient.AddTwinStatusAsync(new ResponseStatus { ModuleId = moduleId, StatusCode = status, ResultAsJson = responseJson, EnqueuedDateTime = DateTime.UtcNow });
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
                receivedTwin = await this.moduleClient.GetTwinAsync();
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to module client get twin: {e}");
                return;
            }

            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Desired;
            Dictionary<string, DateTime> desiredPropertiesUpdated = await this.storage.GetAllDesiredPropertiesUpdated();
            Dictionary<string, DateTime> desiredPropertiesReceived = await this.storage.GetAllDesiredPropertiesReceived();
            Dictionary<string, string> propertiesToRemoveFromTwin = new Dictionary<string, string>();
            foreach (KeyValuePair<string, DateTime> desiredPropertyUpdate in desiredPropertiesUpdated)
            {
                bool doesTwinHaveUpdate = propertyUpdatesFromTwin.Contains(desiredPropertyUpdate.Key);
                bool hasModuleReceivedCallback = desiredPropertiesReceived.ContainsKey(desiredPropertyUpdate.Key);
                string status;
                if (doesTwinHaveUpdate && hasModuleReceivedCallback)
                {
                    status = $"{(int)StatusCode.Success}: Successfully sent/received desired property update";
                    Logger.LogInformation(status + $" {desiredPropertyUpdate.Key}");
                }
                else if (this.IsPastFailureThreshold(desiredPropertyUpdate.Value))
                {
                    if (doesTwinHaveUpdate && !hasModuleReceivedCallback)
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateNoCallbackReceived}: Failure receiving desired property update in callback";
                    }
                    else if (!doesTwinHaveUpdate && hasModuleReceivedCallback)
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateNotInEdgeTwin}: Failure receiving desired property update in twin";
                    }
                    else
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateTotalFailure}: Failure receiving desired property update in both twin and callback";
                    }

                    Logger.LogError(status + $" for update #{desiredPropertyUpdate.Key}");
                }
                else
                {
                    continue;
                }

                await this.CallAnalyzerToReportStatus(Settings.Current.ModuleId, status, string.Empty);
                propertiesToRemoveFromTwin.Add(desiredPropertyUpdate.Key, null); // will later be serialized as a twin update
            }

            foreach (KeyValuePair<string, string> pair in propertiesToRemoveFromTwin)
            {
                    try
                    {
                        await this.storage.RemoveDesiredPropertyUpdate(pair.Key);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to remove validated reported property id {pair.Key} from storage: {e}");
                    }
            }

            try
            {
                string patch = $"{{ properties: {{ desired: {JsonConvert.SerializeObject(propertiesToRemoveFromTwin)} }}";
                Twin newTwin = await this.registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, this.twinState.TwinETag);
                this.twinState.TwinETag = newTwin.ETag;
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
                receivedTwin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                this.twinState.TwinETag = receivedTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to registry manager get twin: {e}");
                this.twinState.LastTimeOffline = DateTime.UtcNow;
                return;
            }

            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Reported;
            Dictionary<string, DateTime> reportedPropertiesUpdated = await this.storage.GetAllReportedPropertiesUpdated();
            TwinCollection propertiesToRemoveFromTwin = new TwinCollection();
            foreach (KeyValuePair<string, DateTime> reportedPropertyUpdate in reportedPropertiesUpdated)
            {
                string status;
                if (propertyUpdatesFromTwin.Contains(reportedPropertyUpdate.Key))
                {
                    try
                    {
                        await this.storage.RemoveReportedPropertyUpdate(reportedPropertyUpdate.Key);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to remove validated reported property id {reportedPropertyUpdate.Key} from storage: {e}");
                        continue;
                    }

                    status = $"{(int)StatusCode.Success}: Successfully sent/received reported property update";
                    Logger.LogInformation(status + $" {reportedPropertyUpdate.Key}");
                }
                else if (this.IsPastFailureThreshold(reportedPropertyUpdate.Value))
                {
                    status = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwin}: Failure receiving reported property update";
                    Logger.LogError(status + $"for reported property update {reportedPropertyUpdate.Key}");
                }
                else
                {
                    continue;
                }

                propertiesToRemoveFromTwin[reportedPropertyUpdate.Key] = null; // will later be serialized as a twin update
                await this.CallAnalyzerToReportStatus(Settings.Current.ModuleId, status, string.Empty);
            }

            foreach (dynamic pair in propertiesToRemoveFromTwin)
            {
                KeyValuePair<string, object> property = (KeyValuePair<string, object>)pair;
                try
                {
                    await this.storage.RemoveReportedPropertyUpdate(property.Key);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Failed to remove validated reported property id {property.Key} from storage: {e}");
                }
            }

            try
            {
                await this.moduleClient.UpdateReportedPropertiesAsync(propertiesToRemoveFromTwin);
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to twin property reset: {e}");
            }
        }

        public async Task PerformDesiredPropertyUpdate()
        {
            try
            {
                string desiredPropertyUpdate = new string('1', Settings.Current.TwinUpdateCharCount); // dummy twin update needs to be any number
                string patch = string.Format("{{ properties: {{ desired: {{ {0}: {1}}} }} }}", this.twinState.DesiredPropertyUpdateCounter, desiredPropertyUpdate);
                Twin newTwin = await this.registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, this.twinState.TwinETag);
                this.twinState.TwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to desired property update: {e}");
                return;
            }

            try
            {
                await this.storage.AddDesiredPropertyUpdate(this.twinState.DesiredPropertyUpdateCounter.ToString());
                this.twinState.DesiredPropertyUpdateCounter += 1;
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
            twin[this.twinState.ReportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdate;
            try
            {
                await this.moduleClient.UpdateReportedPropertiesAsync(twin);
            }
            catch (Exception e)
            {
                string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateCallFailure}: Failed call to update reported properties";
                Logger.LogError(failureStatus + $": {e}");
                await this.CallAnalyzerToReportStatus(Settings.Current.ModuleId, failureStatus, string.Empty);
                return;
            }

            try
            {
                await this.storage.AddReportedPropertyUpdate(this.twinState.ReportedPropertyUpdateCounter.ToString());
                this.twinState.ReportedPropertyUpdateCounter += 1;
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
                await this.storage.AddDesiredPropertyReceived(pair.Key);
            }
        }
    }
}
