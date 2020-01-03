// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class TwinEdgeOperationsInitializer : ITwinTestInitializer
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinCloudOperationsInitializer));
        readonly ReportedPropertyUpdater reportedPropertyUpdater;
        readonly DesiredPropertyReceiver desiredPropertiesReceiver;
        PeriodicTask periodicUpdate;

        TwinEdgeOperationsInitializer(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler reporter, TwinState initializedState)
        {
            this.reportedPropertyUpdater = new ReportedPropertyUpdater(registryManager, moduleClient, reporter, initializedState);
            this.desiredPropertiesReceiver = new DesiredPropertyReceiver(registryManager, moduleClient, reporter);
        }

        public static async Task<TwinEdgeOperationsInitializer> CreateAsync(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler reporter)
        {
            try
            {
                TwinState initializedState;
                Twin twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId);

                // reset properties
                Twin desiredPropertyResetTwin = await registryManager.ReplaceTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, new Twin(), twin.ETag);
                initializedState = new TwinState { TwinETag = desiredPropertyResetTwin.ETag };

                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
                return new TwinEdgeOperationsInitializer(registryManager, moduleClient, reporter, initializedState);
            }
            catch (Exception e)
            {
                throw new Exception($"Shutting down module. Initialization failure: {e}");
            }
        }

        public async Task Start()
        {
            this.periodicUpdate = new PeriodicTask(this.UpdateAsync, Settings.Current.TwinUpdateFrequency, Settings.Current.TwinUpdateFrequency, Logger, "TwinReportedPropertiesUpdate");
            await this.desiredPropertiesReceiver.UpdateAsync();
        }

        public void Dispose()
        {
            this.periodicUpdate.Dispose();
        }

        async Task UpdateAsync(CancellationToken cancellationToken)
        {
            await this.reportedPropertyUpdater.UpdateAsync();
        }
    }
}
