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

    class TwinEdgeOperationsInitializer : ITwinTestInitializer
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinCloudOperationsInitializer));
        readonly ReportedPropertyUpdater reportedPropertyUpdater;
        readonly DesiredPropertyReceiver desiredPropertiesReceiver;
        readonly RegistryManager registryManager;
        PeriodicTask periodicUpdate;

        TwinEdgeOperationsInitializer(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler reporter, int reportedPropertyUpdateCounter)
        {
            this.registryManager = registryManager;
            this.reportedPropertyUpdater = new ReportedPropertyUpdater(moduleClient, reporter, reportedPropertyUpdateCounter);
            this.desiredPropertiesReceiver = new DesiredPropertyReceiver(moduleClient, reporter);
        }

        public static async Task<TwinEdgeOperationsInitializer> CreateAsync(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler reporter)
        {
            try
            {
                Twin twin = await registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);

                // reset reported properties
                await TwinTesterUtil.ResetTwinReportedPropertiesAsync(moduleClient, twin);
                

                return new TwinEdgeOperationsInitializer(registryManager, moduleClient, reporter, 0);
            }
            catch (Exception e)
            {
                throw new Exception($"Shutting down module. Initialization failure: {e}");
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Settings.Current.TestStartDelay, cancellationToken);
            await this.LogEdgeDeviceTwin();
            this.periodicUpdate = new PeriodicTask(this.UpdateAsync, Settings.Current.TwinUpdateFrequency, Settings.Current.TwinUpdateFrequency, Logger, "TwinReportedPropertiesUpdate");
            await this.desiredPropertiesReceiver.UpdateAsync();
        }

        async Task LogEdgeDeviceTwin()
        {
            try
            {
                Twin twin = await this.registryManager.GetTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);
                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failure to get twin");
            }
        }

        public void Stop()
        {
            this.periodicUpdate?.Dispose();
        }

        async Task UpdateAsync(CancellationToken cancellationToken)
        {
            await this.reportedPropertyUpdater.UpdateAsync();
        }
    }
}
