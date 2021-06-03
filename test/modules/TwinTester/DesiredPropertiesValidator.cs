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

    class DesiredPropertiesValidator : ITwinPropertiesValidator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DesiredPropertiesValidator));
        readonly RegistryManager registryManager;
        readonly TwinTestState twinTestState;
        readonly ModuleClient moduleClient;
        readonly TwinEventStorage storage;
        readonly ITwinTestResultHandler resultHandler;

        public DesiredPropertiesValidator(RegistryManager registryManager, ModuleClient moduleClient, TwinEventStorage storage, ITwinTestResultHandler resultHandler, TwinTestState twinTestState)
        {
            this.registryManager = registryManager;
            this.moduleClient = moduleClient;
            this.storage = storage;
            this.resultHandler = resultHandler;
            this.twinTestState = twinTestState;
        }

        public async Task ValidateAsync()
        {
            Twin receivedTwin;
            try
            {
                receivedTwin = await this.moduleClient.GetTwinAsync();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to module client get twin.");
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
                bool hasCallbackReceived = desiredPropertiesReceived.ContainsKey(desiredPropertyUpdate.Key);
                bool isCallbackValidated = hasCallbackReceived || this.SkipCallbackValidationDueToEdgeHubRestart(this.twinTestState, desiredPropertyUpdate.Value);
                string status;

                if (hasTwinUpdate && isCallbackValidated)
                {
                    status = $"{(int)StatusCode.ValidationSuccess}: Successfully validated desired property update";
                    Logger.LogInformation(status + $" {desiredPropertyUpdate.Key}");
                }
                else if (this.ExceedFailureThreshold(this.twinTestState, desiredPropertyUpdate.Value))
                {
                    if (hasTwinUpdate && !isCallbackValidated)
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateNoCallbackReceived}: Failure receiving desired property update for '{desiredPropertyUpdate.Key}' in callback";
                    }
                    else if (!hasTwinUpdate && isCallbackValidated)
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateNotInEdgeTwin}: Failure receiving desired property update for '{desiredPropertyUpdate.Key}' in twin";
                    }
                    else
                    {
                        status = $"{(int)StatusCode.DesiredPropertyUpdateTotalFailure}: Failure receiving desired property update for '{desiredPropertyUpdate.Key}' in both twin and callback";
                    }

                    Logger.LogError($"{status} for update #{desiredPropertyUpdate.Key}");
                }
                else
                {
                    continue;
                }

                await this.HandleReportStatusAsync(status);
                propertiesToRemoveFromTwin.Add(desiredPropertyUpdate.Key, null); // will later be serialized as a twin update
            }

            return propertiesToRemoveFromTwin;
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
                    Logger.LogError(e, $"Failed to remove validated desired property id {pair.Key} from storage.");
                }
            }
        }

        async Task RemovePropertiesFromTwin(Dictionary<string, string> propertiesToRemoveFromTwin)
        {
            try
            {
                string patch = $"{{ properties: {{ desired: {JsonConvert.SerializeObject(propertiesToRemoveFromTwin)} }}";
                Twin newTwin = await this.registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId, patch, this.twinTestState.TwinETag);
                this.twinTestState.TwinETag = newTwin.ETag;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to remove successful desired property updates.");
            }
        }

        bool ExceedFailureThreshold(TwinTestState twinTestState, DateTime twinUpdateTime)
        {
            DateTime comparisonPoint = twinUpdateTime > twinTestState.LastNetworkOffline ? twinUpdateTime : twinTestState.LastNetworkOffline;
            return DateTime.UtcNow - comparisonPoint > Settings.Current.TwinUpdateFailureThreshold;
        }

        bool SkipCallbackValidationDueToEdgeHubRestart(TwinTestState twinTestState, DateTime twinUpdateTime)
        {
            DateTime edgeHubRestartTolerancePeriodLowerBound =
                twinTestState.EdgeHubLastStopped == DateTime.MinValue ? DateTime.MinValue : twinTestState.EdgeHubLastStopped.Add(-Settings.Current.EdgeHubRestartFailureTolerance);
            DateTime edgeHubRestartTolerancePeriodUpperBound =
                twinTestState.EdgeHubLastStarted == DateTime.MinValue ? DateTime.MinValue : twinTestState.EdgeHubLastStarted.Add(Settings.Current.EdgeHubRestartFailureTolerance);

            return edgeHubRestartTolerancePeriodLowerBound <= twinUpdateTime && twinUpdateTime <= edgeHubRestartTolerancePeriodUpperBound;
        }

        async Task HandleReportStatusAsync(string status)
        {
            await this.resultHandler.HandleTwinValidationStatusAsync(status);
        }
    }
}
