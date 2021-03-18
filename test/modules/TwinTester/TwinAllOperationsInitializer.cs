// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class TwinAllOperationsInitializer : ITwinTestInitializer
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinAllOperationsInitializer));
        readonly SemaphoreSlim operationLock = new SemaphoreSlim(1, 1);
        readonly TimeSpan stateUpdatePeriod = TimeSpan.FromSeconds(10);
        readonly ITwinOperation reportedPropertyUpdater;
        readonly ITwinOperation desiredPropertyUpdater;
        readonly ITwinOperation desiredPropertyReceiver;
        readonly ITwinPropertiesValidator reportedPropertiesValidator;
        readonly ITwinPropertiesValidator desiredPropertiesValidator;
        PeriodicTask periodicValidation;
        PeriodicTask periodicUpdate;
        PeriodicTask periodicStateUpdate;
        RegistryManager registryManager;
        TwinTestState twinTestState;

        TwinAllOperationsInitializer(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler resultHandler, TwinEventStorage storage, TwinTestState twinTestState)
        {
            this.registryManager = registryManager;
            this.twinTestState = twinTestState;
            this.reportedPropertyUpdater = new ReportedPropertyUpdater(moduleClient, resultHandler, twinTestState.ReportedPropertyUpdateCounter);
            this.desiredPropertyUpdater = new DesiredPropertyUpdater(registryManager, resultHandler, twinTestState);
            this.desiredPropertyReceiver = new DesiredPropertyReceiver(moduleClient, resultHandler);
            this.reportedPropertiesValidator = new ReportedPropertiesValidator(registryManager, moduleClient, storage, resultHandler, twinTestState);
            this.desiredPropertiesValidator = new DesiredPropertiesValidator(registryManager, moduleClient, storage, resultHandler, twinTestState);

            moduleClient.SetConnectionStatusChangesHandler((status, reason) =>
            {
                Logger.LogInformation($"Detected change in connection status:{Environment.NewLine}Changed Status: {status} Reason: {reason}");
                if (status == ConnectionStatus.Disconnected_Retrying)
                {
                    this.twinTestState.EdgeHubLastStopped = DateTime.UtcNow;
                }
                else if (status == ConnectionStatus.Connected)
                {
                    this.twinTestState.EdgeHubLastStarted = DateTime.UtcNow;
                }
            });
        }

        public static async Task<TwinAllOperationsInitializer> CreateAsync(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler resultHandler, TwinEventStorage storage)
        {
            try
            {
                TwinTestState initializedState;
                Twin twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);
                Dictionary<string, DateTime> reportedPropertyUpdates = await storage.GetAllReportedPropertiesUpdatedAsync();
                Dictionary<string, DateTime> desiredPropertyUpdates = await storage.GetAllDesiredPropertiesUpdatedAsync();

                if (reportedPropertyUpdates.Count == 0 &&
                    desiredPropertyUpdates.Count == 0 &&
                    (await storage.GetAllDesiredPropertiesReceivedAsync()).Count == 0)
                {
                    Logger.LogInformation("No existing storage detected. Initializing new module twin for fresh run.");

                    // reset desired properties
                    Twin desiredPropertyResetTwin = await registryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId, new Twin(), twin.ETag);

                    await TwinTesterUtil.ResetTwinReportedPropertiesAsync(moduleClient, desiredPropertyResetTwin);

                    await Task.Delay(TimeSpan.FromSeconds(10)); // give enough time for reported properties reset to reach cloud
                    twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);
                    initializedState = new TwinTestState(twin.ETag);
                }
                else
                {
                    Logger.LogInformation("Existing storage detected. Initializing reported / desired property update counters.");
                    initializedState = new TwinTestState(
                        GetNewPropertyCounter(reportedPropertyUpdates),
                        GetNewPropertyCounter(desiredPropertyUpdates),
                        twin.ETag,
                        DateTime.MinValue,
                        DateTime.MinValue,
                        DateTime.MinValue);
                }

                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
                return new TwinAllOperationsInitializer(registryManager, moduleClient, resultHandler, storage, initializedState);
            }
            catch (Exception e)
            {
                throw new Exception($"Shutting down module. Initialization failure: {e}");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"Waiting for {Settings.Current.TestStartDelay} based on TestStartDelay setting before starting.");
            await Task.Delay(Settings.Current.TestStartDelay, cancellationToken);
            TimeSpan validationInterval = new TimeSpan(Settings.Current.TwinUpdateFailureThreshold.Ticks / 4);
            this.periodicValidation = new PeriodicTask(this.PerformValidationAsync, validationInterval, validationInterval, Logger, "TwinValidation");
            this.periodicUpdate = new PeriodicTask(this.PerformUpdatesAsync, Settings.Current.TwinUpdateFrequency, Settings.Current.TwinUpdateFrequency, Logger, "TwinUpdates");
            this.periodicStateUpdate = new PeriodicTask(this.UpdateLastNetworkOfflineTimestampAsync, this.stateUpdatePeriod, this.stateUpdatePeriod, Logger, "TwinTestStateUpdate");
            await this.desiredPropertyReceiver.UpdateAsync();
        }

        async Task UpdateLastNetworkOfflineTimestampAsync(CancellationToken arg)
        {
            TimeSpan stateUpdateTimeout = TimeSpan.FromSeconds(5);

            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    cts.CancelAfter(stateUpdateTimeout);
                    await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId, cts.Token);
                }
                catch (Exception e) when (e is IotHubCommunicationException || e is OperationCanceledException || e.InnerException is TaskCanceledException)
                {
                    this.twinTestState.LastNetworkOffline = DateTime.UtcNow;
                }
            }
        }

        async Task PerformUpdatesAsync(CancellationToken cancellationToken)
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

        async Task PerformValidationAsync(CancellationToken cancellationToken)
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

        public void Stop()
        {
            this.periodicValidation?.Dispose();
            this.periodicUpdate?.Dispose();
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
    }
}
