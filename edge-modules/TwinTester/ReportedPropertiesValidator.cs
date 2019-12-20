// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class ReportedPropertiesValidator : ITwinPropertiesValidator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(ReportedPropertiesValidator));
        readonly RegistryManager registryManager;
        readonly TwinState twinState;
        readonly ModuleClient moduleClient;
        readonly TwinEventStorage storage;
        readonly ITwinTestResultHandler reporter;

        public ReportedPropertiesValidator(RegistryManager registryManager, ModuleClient moduleClient, TwinEventStorage storage, ITwinTestResultHandler reporter, TwinState twinState)
        {
            this.registryManager = registryManager;
            this.moduleClient = moduleClient;
            this.storage = storage;
            this.reporter = reporter;
            this.twinState = twinState;
        }

        public async Task ValidateAsync()
        {
            Twin receivedTwin;
            try
            {
                receivedTwin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                this.twinState.TwinETag = receivedTwin.ETag;
            }
            catch (Exception e)
            {
                if (e is IotHubCommunicationException || e is OperationCanceledException) // This is the transient exception case for microsoft.azure.devices.client.deviceclient version 1.21.2
                {
                    Logger.LogError(e, "Failed call to registry manager get twin due to transient error.");
                    this.twinState.LastTimeOffline = DateTime.UtcNow;
                }
                else
                {
                    Logger.LogError(e, "Failed call to registry manager get twin due to non-transient error.");
                }

                return;
            }

            TwinCollection propertiesToRemoveFromTwin = await this.ValidatePropertiesFromTwinAsync(receivedTwin);
            await this.RemovePropertiesFromStorage(propertiesToRemoveFromTwin);
            await this.RemovePropertiesFromTwin(propertiesToRemoveFromTwin);
        }

        async Task RemovePropertiesFromStorage(TwinCollection propertiesToRemoveFromTwin)
        {
            foreach (dynamic pair in propertiesToRemoveFromTwin)
            {
                KeyValuePair<string, object> property = (KeyValuePair<string, object>)pair;
                try
                {
                    await this.storage.RemoveReportedPropertyUpdateAsync(property.Key);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, $"Failed to remove validated reported property id {property.Key} from storage.");
                }
            }
        }

        async Task RemovePropertiesFromTwin(TwinCollection propertiesToRemoveFromTwin)
        {
            try
            {
                await this.moduleClient.UpdateReportedPropertiesAsync(propertiesToRemoveFromTwin);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to twin property reset.");
            }
        }

        async Task<TwinCollection> ValidatePropertiesFromTwinAsync(Twin receivedTwin)
        {
            TwinCollection propertyUpdatesFromTwin = receivedTwin.Properties.Reported;
            Dictionary<string, DateTime> reportedPropertiesUpdated = await this.storage.GetAllReportedPropertiesUpdatedAsync();
            TwinCollection propertiesToRemoveFromTwin = new TwinCollection();
            foreach (KeyValuePair<string, DateTime> reportedPropertyUpdate in reportedPropertiesUpdated)
            {
                string status;
                if (propertyUpdatesFromTwin.Contains(reportedPropertyUpdate.Key))
                {
                    try
                    {
                        await this.storage.RemoveReportedPropertyUpdateAsync(reportedPropertyUpdate.Key);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, $"Failed to remove validated reported property id {reportedPropertyUpdate.Key} from storage.");
                        continue;
                    }

                    status = $"{(int)StatusCode.ValidationSuccess}: Successfully validated reported property update";
                    Logger.LogInformation(status + $" {reportedPropertyUpdate.Key}");
                }
                else if (this.ExceedFailureThreshold(this.twinState, reportedPropertyUpdate.Value))
                {
                    status = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwin}: Failure receiving reported property update";
                    Logger.LogInformation($"{status} for reported property update {reportedPropertyUpdate.Key}");
                }
                else
                {
                    continue;
                }

                propertiesToRemoveFromTwin[reportedPropertyUpdate.Key] = null; // will later be serialized as a twin update
                await this.HandleReportStatusAsync(Settings.Current.ModuleId, status);
            }

            return propertiesToRemoveFromTwin;
        }

        bool ExceedFailureThreshold(TwinState twinState, DateTime twinUpdateTime)
        {
            DateTime comparisonPoint = twinUpdateTime > twinState.LastTimeOffline ? twinUpdateTime : twinState.LastTimeOffline;
            return DateTime.UtcNow - comparisonPoint > Settings.Current.TwinUpdateFailureThreshold;
        }

        async Task HandleReportStatusAsync(string moduleId, string status)
        {
            await this.reporter.HandleTwinValidationStatusAsync(status);
        }
    }
}
