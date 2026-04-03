// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;

    class ReportedPropertiesValidator : ITwinPropertiesValidator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(ReportedPropertiesValidator));
        readonly IotHubServiceClient serviceClient;
        readonly TwinTestState twinTestState;
        readonly IotHubModuleClient moduleClient;
        readonly TwinEventStorage storage;
        readonly ITwinTestResultHandler reporter;
        HashSet<string> reportedPropertyKeysFailedToReceiveWithinThreshold = new HashSet<string>();

        public ReportedPropertiesValidator(IotHubServiceClient serviceClient, IotHubModuleClient moduleClient, TwinEventStorage storage, ITwinTestResultHandler reporter, TwinTestState twinTestState)
        {
            this.serviceClient = serviceClient;
            this.moduleClient = moduleClient;
            this.storage = storage;
            this.reporter = reporter;
            this.twinTestState = twinTestState;
        }

        public async Task ValidateAsync()
        {
            ClientTwin receivedTwin;
            try
            {
                receivedTwin = await this.serviceClient.Twins.GetAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);
                this.twinTestState.TwinETag = receivedTwin.ETag.ToString();
            }
            catch (Exception e)
            {
                if (e is IotHubServiceException || e is OperationCanceledException) // Transient exception case
                {
                    Logger.LogError(e, "Failed call to service client get twin due to transient error.");
                }
                else
                {
                    Logger.LogError(e, "Failed call to service client get twin due to non-transient error.");
                }

                return;
            }

            PropertyCollection propertiesToRemove = await this.ValidatePropertiesFromTwinAsync(receivedTwin);
            await this.RemovePropertiesFromStorage(propertiesToRemove);
            await this.RemovePropertiesFromTwin(propertiesToRemove);
        }

        async Task RemovePropertiesFromStorage(PropertyCollection propertiesToRemove)
        {
            foreach (KeyValuePair<string, object> property in propertiesToRemove)
            {
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

        async Task RemovePropertiesFromTwin(PropertyCollection propertiesToRemove)
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

        async Task<PropertyCollection> ValidatePropertiesFromTwinAsync(ClientTwin receivedTwin)
        {
            ClientTwinProperties propertyUpdatesFromTwin = receivedTwin.Properties.Reported;
            Dictionary<string, DateTime> reportedPropertiesUpdated = await this.storage.GetAllReportedPropertiesUpdatedAsync();
            PropertyCollection propertiesToRemove = new PropertyCollection();

            foreach (KeyValuePair<string, DateTime> reportedPropertyUpdate in reportedPropertiesUpdated)
            {
                string status;
                if (propertyUpdatesFromTwin.ContainsKey(reportedPropertyUpdate.Key))
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
                        this.DoesExceedFailureThreshold(this.twinTestState, reportedPropertyUpdate.Key, reportedPropertyUpdate.Value))
                {
                    this.reportedPropertyKeysFailedToReceiveWithinThreshold.Add(reportedPropertyUpdate.Key);
                    status = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwin}: Failure receiving reported property update";
                    await this.HandleReportStatusAsync(status);
                    Logger.LogInformation($"{status} for reported property update {reportedPropertyUpdate.Key}");
                }
                else if (this.DoesExceedCleanupThreshold(this.twinTestState, reportedPropertyUpdate.Key, reportedPropertyUpdate.Value))
                {
                    status = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwinAfterCleanUpThreshold}: Failure receiving reported property update after cleanup threshold";
                    await this.HandleReportStatusAsync(status);
                    Logger.LogInformation($"{status} for reported property update {reportedPropertyUpdate.Key}");
                    propertiesToRemove[reportedPropertyUpdate.Key] = null; // will later be serialized as a twin update
                }
            }

            return propertiesToRemove;
        }

        bool DoesExceedFailureThreshold(TwinTestState twinState, string reportedPropertyKey, DateTime reportedPropertyUpdatedAt)
        {
            DateTime comparisonPoint = reportedPropertyUpdatedAt > twinState.LastNetworkOffline ? reportedPropertyUpdatedAt : twinState.LastNetworkOffline;
            bool exceedFailureThreshold = DateTime.UtcNow - comparisonPoint > Settings.Current.TwinUpdateFailureThreshold;

            if (exceedFailureThreshold)
            {
                Logger.LogInformation(
                    $"Reported Property [{reportedPropertyKey}] exceed failure threshold: updated at={reportedPropertyUpdatedAt}, " +
                    $"last offline={twinState.LastNetworkOffline}, threshold={Settings.Current.TwinUpdateFailureThreshold}");
            }

            return exceedFailureThreshold;
        }

        bool DoesExceedCleanupThreshold(TwinTestState twinState, string reportedPropertyKey, DateTime reportedPropertyUpdatedAt)
        {
            TimeSpan cleanUpThreshold = TimeSpan.FromMinutes(5);
            DateTime comparisonPoint = reportedPropertyUpdatedAt > twinState.LastNetworkOffline ? reportedPropertyUpdatedAt : twinState.LastNetworkOffline;
            bool exceedCleanupThreshold = DateTime.UtcNow - comparisonPoint > (Settings.Current.TwinUpdateFailureThreshold + cleanUpThreshold);

            if (exceedCleanupThreshold)
            {
                Logger.LogInformation(
                    $"Reported Property [{reportedPropertyKey}] exceed cleanup threshold: updated at={reportedPropertyUpdatedAt}, " +
                    $"last offline={twinState.LastNetworkOffline}, threshold={Settings.Current.TwinUpdateFailureThreshold + cleanUpThreshold}");
            }

            return exceedCleanupThreshold;
        }

        async Task HandleReportStatusAsync(string status)
        {
            await this.reporter.HandleTwinValidationStatusAsync(status);
        }
    }
}
