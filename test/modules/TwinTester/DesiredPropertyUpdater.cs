// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class DesiredPropertyUpdater : ITwinOperation
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DesiredPropertyUpdater));
        readonly RegistryManager registryManager;
        readonly ITwinTestResultHandler resultHandler;
        readonly TwinTestState twinTestState;
        int desiredPropertyUpdateCounter;

        public DesiredPropertyUpdater(RegistryManager registryManager, ITwinTestResultHandler resultHandler, TwinTestState twinTestState)
        {
            this.registryManager = registryManager;
            this.resultHandler = resultHandler;
            this.twinTestState = twinTestState;
            this.desiredPropertyUpdateCounter = twinTestState.DesiredPropertyUpdateCounter;
        }

        public async Task UpdateAsync()
        {
            try
            {
                string desiredPropertyUpdateValue = new string('1', Settings.Current.TwinUpdateSize); // dummy twin update can be any character

                var desiredProperties = new TwinProperties();
                string propertyKey = this.desiredPropertyUpdateCounter.ToString();
                desiredProperties.Desired[propertyKey] = desiredPropertyUpdateValue;
                Twin patch = new Twin(desiredProperties);

                Twin newTwin = await this.registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId, patch, this.twinTestState.TwinETag);
                this.twinTestState.TwinETag = newTwin.ETag;

                Logger.LogInformation($"Desired property updated {propertyKey}");

                await this.Report(propertyKey, desiredPropertyUpdateValue);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed call to desired property update.");
            }
        }

        async Task Report(string propertyKey, string value)
        {
            try
            {
                await this.resultHandler.HandleDesiredPropertyUpdateAsync(propertyKey, value);
                this.desiredPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed reporting desired property update for {propertyKey}.");
            }
        }
    }
}
