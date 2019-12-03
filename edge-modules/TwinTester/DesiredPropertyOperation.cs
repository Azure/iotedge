// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class DesiredPropertyOperation : TwinOperationBase
    {
        static readonly ILogger LoggerImpl = ModuleUtil.CreateLogger(nameof(DesiredPropertyOperation));
        readonly RegistryManager registryManager;
        readonly ModuleClient moduleClient;
        readonly AnalyzerClient analyzerClient;
        readonly TwinEventStorage storage;
        readonly TwinState twinState;

        public DesiredPropertyOperation(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, TwinEventStorage storage, TwinState twinState)
        {
            this.registryManager = registryManager;
            this.moduleClient = moduleClient;
            this.analyzerClient = analyzerClient;
            this.storage = storage;
            this.twinState = twinState;
            this.moduleClient.SetDesiredPropertyUpdateCallbackAsync(this.OnDesiredPropertyUpdateAsync, storage);
        }

        public override ILogger Logger => LoggerImpl;

        public override async Task UpdateAsync()
        {
            try
            {
                string desiredPropertyUpdateValue = new string('1', Settings.Current.TwinUpdateSize); // dummy twin update needs to be any number
                string patch = string.Format("{{ properties: {{ desired: {{ {0}: {1}}} }} }}", this.twinState.DesiredPropertyUpdateCounter, desiredPropertyUpdateValue);
                Twin newTwin = await this.registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, this.twinState.TwinETag);
                this.twinState.TwinETag = newTwin.ETag;
                this.Logger.LogInformation($"Made desired property update {this.twinState.DesiredPropertyUpdateCounter}");
            }
            catch (Exception e)
            {
                this.Logger.LogInformation($"Failed call to desired property update: {e}");
                return;
            }

            try
            {
                await this.storage.AddDesiredPropertyUpdateAsync(this.twinState.DesiredPropertyUpdateCounter.ToString());
                this.twinState.DesiredPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                this.Logger.LogError($"Failed adding desired property update to storage: {e}");
            }
        }

        public override async Task ValidateAsync()
        {
            Twin receivedTwin;
            try
            {
                receivedTwin = await this.moduleClient.GetTwinAsync();
            }
            catch (Exception e)
            {
                this.Logger.LogInformation($"Failed call to module client get twin: {e}");
                return;
            }

            Dictionary<string, string> propertiesToRemoveFromTwin = await this.ValidatePropertiesFromTwinAsync(receivedTwin);
            await this.RemovePropertiesFromStorage(propertiesToRemoveFromTwin);
            await this.RemovePropertiesFromTwin(propertiesToRemoveFromTwin);
        }

        async Task<Dictionary<string, string>> ValidatePropertiesFromTwinAsync(Twin receivedTwin)
        {
            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Desired;
            Dictionary<string, DateTime> desiredPropertiesUpdated = await this.storage.GetAllDesiredPropertiesUpdatedAsync();
            Dictionary<string, DateTime> desiredPropertiesReceived = await this.storage.GetAllDesiredPropertiesReceivedAsync();
            Dictionary<string, string> propertiesToRemoveFromTwin = new Dictionary<string, string>();
            foreach (KeyValuePair<string, DateTime> desiredPropertyUpdate in desiredPropertiesUpdated)
            {
                bool hasTwinUpdate = propertyUpdatesFromTwin.Contains(desiredPropertyUpdate.Key);
                bool hasModuleReceivedCallback = desiredPropertiesReceived.ContainsKey(desiredPropertyUpdate.Key);
                string status;
                if (hasTwinUpdate && hasModuleReceivedCallback)
                {
                    status = $"{(int)StatusCode.Success}: Successfully validated desired property update";
                    this.Logger.LogInformation(status + $" {desiredPropertyUpdate.Key}");
                }
                else if (this.ExceedFailureThreshold(this.twinState, desiredPropertyUpdate.Value))
                {
                    if (hasTwinUpdate && !hasModuleReceivedCallback)
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateNoCallbackReceived}: Failure receiving desired property update in callback";
                    }
                    else if (!hasTwinUpdate && hasModuleReceivedCallback)
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateNotInEdgeTwin}: Failure receiving desired property update in twin";
                    }
                    else
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateTotalFailure}: Failure receiving desired property update in both twin and callback";
                    }

                    this.Logger.LogError(status + $" for update #{desiredPropertyUpdate.Key}");
                }
                else
                {
                    continue;
                }

                await this.CallAnalyzerToReportStatusAsync(this.analyzerClient, Settings.Current.ModuleId, status);
                propertiesToRemoveFromTwin.Add(desiredPropertyUpdate.Key, null); // will later be serialized as a twin update
            }

            return propertiesToRemoveFromTwin;
        }

        async Task OnDesiredPropertyUpdateAsync(TwinCollection desiredProperties, object userContext)
        {
            TwinEventStorage storage = (TwinEventStorage)userContext;
            foreach (dynamic twinUpdate in desiredProperties)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                await this.storage.AddDesiredPropertyReceivedAsync(pair.Key);
            }
        }

        async Task RemovePropertiesFromStorage(Dictionary<string, string> propertiesToRemoveFromTwin)
        {
            foreach (KeyValuePair<string, string> pair in propertiesToRemoveFromTwin)
            {
                try
                {
                    await this.storage.RemoveDesiredPropertyUpdateAsync(pair.Key);
                    await this.storage.RemoveDesiredPropertyReceivedAsync(pair.Key);
                }
                catch (Exception e)
                {
                    this.Logger.LogError($"Failed to remove validated desired property id {pair.Key} from storage: {e}");
                }
            }
        }

        async Task RemovePropertiesFromTwin(Dictionary<string, string> propertiesToRemoveFromTwin)
        {
            try
            {
                string patch = $"{{ properties: {{ desired: {JsonConvert.SerializeObject(propertiesToRemoveFromTwin)} }}";
                Twin newTwin = await this.registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, this.twinState.TwinETag);
                this.twinState.TwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                this.Logger.LogInformation($"Failed call to remove successful desired property updates: {e}");
            }
        }
    }
}
