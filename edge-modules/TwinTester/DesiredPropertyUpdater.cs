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
        readonly IResultHandler resultHandler;
        readonly TwinState twinState;

        public DesiredPropertyUpdater(RegistryManager registryManager, IResultHandler resultHandler, TwinState twinState)
        {
            this.registryManager = registryManager;
            this.resultHandler = resultHandler;
            this.twinState = twinState;
        }

        public async Task UpdateAsync()
        {
            try
            {
                string desiredPropertyUpdateValue = new string('1', Settings.Current.TwinUpdateSize); // dummy twin update can to be any char

                var desiredProperties = new TwinProperties();
                string propertyKey = this.twinState.DesiredPropertyUpdateCounter.ToString();
                desiredProperties.Desired[propertyKey] = desiredPropertyUpdateValue;
                Twin patch = new Twin(desiredProperties);

                Twin newTwin = await this.registryManager.UpdateTwinAsync(Settings.Current.DeviceId, Settings.Current.ModuleId, patch, this.twinState.TwinETag);
                this.twinState.TwinETag = newTwin.ETag;

                Logger.LogInformation($"Desired property updated {propertyKey}");

                await this.Report(propertyKey);
            }
            catch (Exception e)
            {
                Logger.LogInformation($"Failed call to desired property update: {e}");
                return;
            }
        }

        async Task Report(string propertyKey)
        {
            try
            {
                await this.resultHandler.HandleDesiredPropertyUpdateAsync(propertyKey);
                this.twinState.DesiredPropertyUpdateCounter += 1;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed reporting desired property update for {propertyKey}: {e}");
            }
        }
    }
}
