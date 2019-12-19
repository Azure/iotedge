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

    class TwinAllOperationsInitializer : ITwinTestInitializer
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinAllOperationsInitializer));
        readonly SemaphoreSlim operationLock = new SemaphoreSlim(1, 1);
        readonly ITwinOperation reportedPropertyUpdater;
        readonly ITwinOperation desiredPropertyUpdater;
        readonly ITwinOperation desiredPropertyReceiver;
        readonly ITwinPropertiesValidator reportedPropertiesValidator;
        readonly ITwinPropertiesValidator desiredPropertiesValidator;
        PeriodicTask periodicValidation;
        PeriodicTask periodicUpdate;

        TwinAllOperationsInitializer(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler resultHandler, TwinEventStorage storage, TwinState twinState)
        {
            this.reportedPropertyUpdater = new ReportedPropertyUpdater(registryManager, moduleClient, resultHandler, twinState);
            this.desiredPropertyUpdater = new DesiredPropertyUpdater(registryManager, resultHandler, twinState);
            this.desiredPropertyReceiver = new DesiredPropertyReceiver(registryManager, moduleClient, resultHandler);
            this.reportedPropertiesValidator = new ReportedPropertiesValidator(registryManager, moduleClient, storage, resultHandler, twinState);
            this.desiredPropertiesValidator = new DesiredPropertiesValidator(registryManager, moduleClient, storage, resultHandler, twinState);
        }

        public static async Task<TwinAllOperationsInitializer> CreateAsync(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler resultHandler, TwinEventStorage storage)
        {
            try
            {
                TwinState initializedState;
                Twin twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                Dictionary<string, DateTime> reportedPropertyUpdates = await storage.GetAllReportedPropertiesUpdatedAsync();
                Dictionary<string, DateTime> desiredPropertyUpdates = await storage.GetAllDesiredPropertiesUpdatedAsync();

                if (reportedPropertyUpdates.Count == 0 &&
                    desiredPropertyUpdates.Count == 0 &&
                    (await storage.GetAllDesiredPropertiesReceivedAsync()).Count == 0)
                {
                    Logger.LogInformation("No existing storage detected. Initializing new module twin for fresh run.");

                    // reset desired properties
                    Twin desiredPropertyResetTwin = await registryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, new Twin(), twin.ETag);

                    // reset reported properties
                    TwinCollection eraseReportedProperties = GetReportedPropertiesResetTwin(desiredPropertyResetTwin);
                    await moduleClient.UpdateReportedPropertiesAsync(eraseReportedProperties);

                    await Task.Delay(TimeSpan.FromSeconds(10)); // give enough time for reported properties reset to reach cloud
                    twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);
                    initializedState = new TwinState { ReportedPropertyUpdateCounter = 0, DesiredPropertyUpdateCounter = 0, TwinETag = twin.ETag, LastTimeOffline = DateTime.MinValue };
                }
                else
                {
                    Logger.LogInformation("Existing storage detected. Initializing reported / desired property update counters.");
                    initializedState = new TwinState { ReportedPropertyUpdateCounter = GetNewPropertyCounter(reportedPropertyUpdates), DesiredPropertyUpdateCounter = GetNewPropertyCounter(desiredPropertyUpdates), TwinETag = twin.ETag, LastTimeOffline = DateTime.MinValue };
                }

                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
                return new TwinAllOperationsInitializer(registryManager, moduleClient, resultHandler, storage, initializedState);
            }
            catch (Exception e)
            {
                throw new Exception($"Shutting down module. Initialization failure: {e}");
            }
        }

        public Task Start()
        {
            TimeSpan validationInterval = new TimeSpan(Settings.Current.TwinUpdateFailureThreshold.Ticks / 4);
            this.periodicValidation = new PeriodicTask(this.PerformValidationAsync, validationInterval, validationInterval, Logger, "TwinValidation");
            this.periodicUpdate = new PeriodicTask(this.PerformUpdatesAsync, Settings.Current.TwinUpdateFrequency, Settings.Current.TwinUpdateFrequency, Logger, "TwinUpdates");
            return this.desiredPropertyReceiver.UpdateAsync();
        }

        public async Task PerformUpdatesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.operationLock.WaitAsync();
                await this.reportedPropertyUpdater.UpdateAsync();
                await this.desiredPropertyUpdater.UpdateAsync();
            }
            finally
            {
                this.operationLock.Release();
            }
        }

        public async Task PerformValidationAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.operationLock.WaitAsync();
                await this.reportedPropertiesValidator.ValidateAsync();
                await this.desiredPropertiesValidator.ValidateAsync();
            }
            finally
            {
                this.operationLock.Release();
            }
        }

        public void Dispose()
        {
            this.periodicValidation.Dispose();
            this.periodicUpdate.Dispose();
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
    }
}
