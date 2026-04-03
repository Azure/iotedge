// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class TwinCloudOperationsInitializer : ITwinTestInitializer
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TwinCloudOperationsInitializer));
        readonly DesiredPropertyUpdater desiredPropertyUpdater;
        PeriodicTask periodicUpdate;

        TwinCloudOperationsInitializer(IotHubServiceClient serviceClient, ITwinTestResultHandler resultHandler, TwinTestState twinTestState)
        {
            this.desiredPropertyUpdater = new DesiredPropertyUpdater(serviceClient, resultHandler, twinTestState);
        }

        public static async Task<TwinCloudOperationsInitializer> CreateAsync(IotHubServiceClient serviceClient, ITwinTestResultHandler resultHandler)
        {
            try
            {
                TwinTestState initializedState;
                ClientTwin twin = await serviceClient.Twins.GetAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId);

                initializedState = new TwinTestState(twin.ETag.ToString());

                Logger.LogInformation($"Start state of module twin: {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
                return new TwinCloudOperationsInitializer(serviceClient, resultHandler, initializedState);
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
