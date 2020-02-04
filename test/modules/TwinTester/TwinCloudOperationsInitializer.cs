// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class TwinCloudOperationsInitializer : ITwinTestInitializer
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinCloudOperationsInitializer));
        readonly DesiredPropertyUpdater desiredPropertyUpdater;
        PeriodicTask periodicUpdate;

        TwinCloudOperationsInitializer(RegistryManager registryManager, ITwinTestResultHandler resultHandler, TwinState twinState)
        {
            this.desiredPropertyUpdater = new DesiredPropertyUpdater(registryManager, resultHandler, twinState);
        }

        public static async Task<TwinCloudOperationsInitializer> CreateAsync(RegistryManager registryManager, ITwinTestResultHandler resultHandler)
        {
            try
            {
                TwinState initializedState;
                Twin twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);

                // reset desired properties
                twin.Properties.Desired = null;
                Twin desiredPropertyResetTwin = await registryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId, twin, twin.ETag);

                initializedState = new TwinState { TwinETag = desiredPropertyResetTwin.ETag };

                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(desiredPropertyResetTwin, Formatting.Indented)}");
                return new TwinCloudOperationsInitializer(registryManager, resultHandler, initializedState);
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
            this.periodicUpdate = new PeriodicTask(this.UpdateAsync, Settings.Current.TwinUpdateFrequency, Settings.Current.TwinUpdateFrequency, Logger, "TwinDesiredPropertiesUpdate");
        }

        public async Task UpdateAsync(CancellationToken cancellationToken)
        {
            await this.desiredPropertyUpdater.UpdateAsync();
        }

        public void Stop()
        {
            this.periodicUpdate?.Dispose();
        }
    }
}
