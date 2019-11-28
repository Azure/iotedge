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

    public class ReportedPropertyOperation : TwinOperationBase
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("ReportedPropertyOperation");
        readonly RegistryManager registryManager;
        readonly ModuleClient moduleClient;
        readonly AnalyzerClient analyzerClient;
        readonly TwinEventStorage storage;
        TwinState twinState;

        public ReportedPropertyOperation(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, TwinEventStorage storage, TwinState twinState)
        {
            this.registryManager = registryManager;
            this.moduleClient = moduleClient;
            this.analyzerClient = analyzerClient;
            this.storage = storage;
            this.twinState = twinState;
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
                        Logger.LogError($"Failed to remove validated reported property id {reportedPropertyUpdate.Key} from storage: {e}");
                        continue;
                    }

                    status = $"{(int)StatusCode.Success}: Successfully validated reported property update";
                    Logger.LogInformation(status + $" {reportedPropertyUpdate.Key}");
                }
                else if (TwinOperationBase.IsPastFailureThreshold(this.twinState, reportedPropertyUpdate.Value))
                {
                    status = $"{(int)StatusCode.ReportedPropertyUpdateNotInCloudTwin}: Failure receiving reported property update";
                    Logger.LogError(status + $" for reported property update {reportedPropertyUpdate.Key}");
                }
                else
                {
                    continue;
                }

                propertiesToRemoveFromTwin[reportedPropertyUpdate.Key] = null; // will later be serialized as a twin update
                await TwinOperationBase.CallAnalyzerToReportStatusAsync(this.analyzerClient, Settings.Current.ModuleId, status);
            }

            return propertiesToRemoveFromTwin;
        }

        public override async Task PerformValidationAsync()
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

            TwinCollection propertiesToRemoveFromTwin = await this.ValidatePropertiesFromTwinAsync(receivedTwin);
            foreach (dynamic pair in propertiesToRemoveFromTwin)
            {
                KeyValuePair<string, object> property = (KeyValuePair<string, object>)pair;
                try
                {
                    await this.storage.RemoveReportedPropertyUpdateAsync(property.Key);
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

        public override async Task PerformUpdateAsync()
        {
            string reportedPropertyUpdate = new string('1', Settings.Current.TwinUpdateSize); // dummy twin update needs to be any number
            var twin = new TwinCollection();
            twin[this.twinState.ReportedPropertyUpdateCounter.ToString()] = reportedPropertyUpdate;
            try
            {
                await this.moduleClient.UpdateReportedPropertiesAsync(twin);
                Logger.LogInformation($"Made reported property update {this.twinState.ReportedPropertyUpdateCounter}");
            }
            catch (Exception e)
            {
                string failureStatus = $"{(int)StatusCode.ReportedPropertyUpdateCallFailure}: Failed call to update reported properties";
                Logger.LogError(failureStatus + $": {e}");
                await TwinOperationBase.CallAnalyzerToReportStatusAsync(this.analyzerClient, Settings.Current.ModuleId, failureStatus);
                return;
            }

            try
            {
                await this.storage.AddReportedPropertyUpdateAsync(this.twinState.ReportedPropertyUpdateCounter.ToString());
                this.twinState.ReportedPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed adding reported property update to storage: {e}");
                return;
            }
        }
    }
}
