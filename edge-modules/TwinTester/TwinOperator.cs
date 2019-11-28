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
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class TwinOperator
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("TwinOperator");
        readonly SemaphoreSlim operationLock = new SemaphoreSlim(1, 1);
        readonly ReportedPropertyOperation reportedPropertyOperation;
        readonly DesiredPropertyOperation desiredPropertyOperation;

        public TwinOperator(RegistryManager registryManager, ModuleClient moduleClient, AnalyzerClient analyzerClient, TwinEventStorage storage, TwinState twinState)
        {
            this.reportedPropertyOperation = new ReportedPropertyOperation(registryManager, moduleClient, analyzerClient, storage, twinState);
            this.desiredPropertyOperation = new DesiredPropertyOperation(registryManager, moduleClient, analyzerClient, storage, twinState);
        }

        static int GetNewPropertyCounter(Dictionary<string, DateTime> properties)
        {
            int maxPropertyId = -1;
            foreach (KeyValuePair<string, DateTime> propertyUpdate in properties)
            {
                maxPropertyId = Math.Max(int.Parse(propertyUpdate.Key), maxPropertyId);
            }

            return maxPropertyId + 1;
        }

        static TwinCollection GetReportedPropertiesResetTwin(Twin originalTwin)
        {
            TwinCollection eraseReportedProperties = new TwinCollection();
            foreach (dynamic twinUpdate in originalTwin.Properties.Reported)
            {
                KeyValuePair<string, object> pair = (KeyValuePair<string, object>)twinUpdate;
                eraseReportedProperties[pair.Key] = null; // erase twin property by assigning null
            }

            return eraseReportedProperties;
        }

        public static async Task<TwinState> InitializeModuleTwinAsync(RegistryManager registryManager, ModuleClient moduleClient, TwinEventStorage storage)
        {
            try
            {
                TwinState initializedState;
                Twin twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                int storageCount = (await storage.GetAllDesiredPropertiesReceivedAsync()).Count + (await storage.GetAllDesiredPropertiesUpdatedAsync()).Count + (await storage.GetAllReportedPropertiesUpdatedAsync()).Count;
                if (storageCount == 0)
                {
                    Logger.LogInformation("No existing storage detected. Initializing new module twin for fresh run.");

                    // reset desired properties
                    Twin desiredPropertyResetTwin = await registryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, new Twin(), twin.ETag);

                    // reset reported properties
                    TwinCollection eraseReportedProperties = GetReportedPropertiesResetTwin(desiredPropertyResetTwin);
                    await moduleClient.UpdateReportedPropertiesAsync(eraseReportedProperties);

                    await Task.Delay(TimeSpan.FromSeconds(10)); // give ample time for reported properties reset to reach cloud
                    twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                    initializedState = new TwinState { ReportedPropertyUpdateCounter = 0, DesiredPropertyUpdateCounter = 0, TwinETag = twin.ETag, LastTimeOffline = DateTime.MinValue };
                }
                else
                {
                    Logger.LogInformation("Existing storage detected. Initializing reported / desired property update counters.");
                    Dictionary<string, DateTime> reportedProperties = await storage.GetAllReportedPropertiesUpdatedAsync();
                    Dictionary<string, DateTime> desiredProperties = await storage.GetAllDesiredPropertiesUpdatedAsync();
                    initializedState = new TwinState { ReportedPropertyUpdateCounter = GetNewPropertyCounter(reportedProperties), DesiredPropertyUpdateCounter = GetNewPropertyCounter(desiredProperties), TwinETag = twin.ETag, LastTimeOffline = DateTime.MinValue };
                }

                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
                return initializedState;
            }
            catch (Exception e)
            {
                throw new Exception($"Shutting down module. Initialization failure: {e}");
            }
        }

        public async Task PerformUpdatesAsync(CancellationToken cancellationToken)
        {
            await this.operationLock.WaitAsync();
            await this.reportedPropertyOperation.PerformUpdateAsync();
            await this.desiredPropertyOperation.PerformUpdateAsync();
            this.operationLock.Release();
        }

        public async Task PerformValidationAsync(CancellationToken cancellationToken)
        {
            await this.operationLock.WaitAsync();
            await this.reportedPropertyOperation.PerformValidationAsync();
            await this.desiredPropertyOperation.PerformValidationAsync();
            this.operationLock.Release();
        }
    }
}
