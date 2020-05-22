// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Exceptions;
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
        HashSet<string> reportedPropertyKeysFailedToReceiveWithinThreshold = new HashSet<string>();

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
                receivedTwin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);
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

            TwinCollection propertiesToRemove = await this.ValidatePropertiesFromTwinAsync(receivedTwin);
            await this.RemovePropertiesFromStorage(propertiesToRemove);
            await this.RemovePropertiesFromTwin(propertiesToRemove);
        }

        async Task RemovePropertiesFromStorage(TwinCollection propertiesToRemove)
        {
            foreach (dynamic pair in propertiesToRemove)
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

        async Task RemovePropertiesFromTwin(TwinCollection propertiesToRemove)
        {
            try
            {
                await this.moduleClient.UpdateReportedPropertiesAsync(propertiesToRemove);
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
            TwinCollection propertiesToRemove = new TwinCollection();

            foreach (KeyValuePair<string, DateTime> reportedPropertyUpdate in reportedPropertiesUpdated)
            {
                string status;
                if (propertyUpdatesFromTwin.Contains(reportedPropertyUpdate.Key))
                {
                    if (this.reportedPropertyKeysFailedToReceiveWithinThreshold.Contains(reportedPropertyUpdate.Key))
                    {
                        this.reportedPropertyKeysFailedToReceiveWithinThreshold.Remove(reportedPropertyUpdate.Key);
                        status = $"{(int)StatusCode.ReportedPropertyReceivedAfterThreshold}: Successfully validated reported property update (exceeded failure threshold)";
                    }
                    else
                    {
                        status = $"{(int)StatusCode.ValidationSuccess}: Successfully validated reported property update";
                    }

                    await this.HandleReportStatusAsync(status);
                    Logger.LogInformation($"{status} for reported property update {reportedPropertyUpdate.Key}");
                    propertiesToRemove[reportedPropertyUpdate.Key] = null; // will later be serialized as a twin update
                }
                else if (!this.reportedPropertyKeysFailedToReceiveWithinThreshold.Contains(reportedPropertyUpdate.Key) &&
                        this.DoesExceedFailureThreshold(this.twinState, reportedPropertyUpdate.Key, reportedPropertyUpdate.Value))
                {
                    this.reportedPropertyKeysFailedToReceiveWithinThreshold.Add(reportedPropertyUpdate.Key);
                    status = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwin}: Failure receiving reported property update";
                    await this.HandleReportStatusAsync(status);
                    Logger.LogInformation($"{status} for reported property update {reportedPropertyUpdate.Key}");
                }
                else if (this.DoesExceedCleanupThreshold(this.twinState, reportedPropertyUpdate.Key, reportedPropertyUpdate.Value))
                {
                    status = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwinAfterCleanUpThreshold}: Failure receiving reported property update after cleanup threshold";
                    await this.HandleReportStatusAsync(status);
                    Logger.LogInformation($"{status} for reported property update {reportedPropertyUpdate.Key}");
                    propertiesToRemove[reportedPropertyUpdate.Key] = null; // will later be serialized as a twin update
                }
            }

            return propertiesToRemove;
        }

        bool DoesExceedFailureThreshold(TwinState twinState, string reportedPropertyKey, DateTime reportedPropertyUpdatedAt)
        {
            DateTime comparisonPoint = reportedPropertyUpdatedAt > twinState.LastTimeOffline ? reportedPropertyUpdatedAt : twinState.LastTimeOffline;
            bool exceedFailureThreshold = DateTime.UtcNow - comparisonPoint > Settings.Current.TwinUpdateFailureThreshold;

            if (exceedFailureThreshold)
            {
                Logger.LogInformation(
                    $"Reported Property [{reportedPropertyKey}] exceed failure threshold: updated at={reportedPropertyUpdatedAt}, " +
                    $"last offline={twinState.LastTimeOffline}, threshold={Settings.Current.TwinUpdateFailureThreshold}");
            }

            return exceedFailureThreshold;
        }

        bool DoesExceedCleanupThreshold(TwinState twinState, string reportedPropertyKey, DateTime reportedPropertyUpdatedAt)
        {
            TimeSpan cleanUpThreshold = TimeSpan.FromMinutes(5);
            DateTime comparisonPoint = reportedPropertyUpdatedAt > twinState.LastTimeOffline ? reportedPropertyUpdatedAt : twinState.LastTimeOffline;
            bool exceedCleanupThreshold = DateTime.UtcNow - comparisonPoint > (Settings.Current.TwinUpdateFailureThreshold + cleanUpThreshold);

            if (exceedCleanupThreshold)
            {
                Logger.LogInformation(
                    $"Reported Property [{reportedPropertyKey}] exceed cleanup threshold: updated at={reportedPropertyUpdatedAt}, " +
                    $"last offline={twinState.LastTimeOffline}, threshold={Settings.Current.TwinUpdateFailureThreshold + cleanUpThreshold}");
            }

            return exceedCleanupThreshold;
        }

        async Task HandleReportStatusAsync(string status)
        {
            await this.reporter.HandleTwinValidationStatusAsync(status);
        }
    }
}
