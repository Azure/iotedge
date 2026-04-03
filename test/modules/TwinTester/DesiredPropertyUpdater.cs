// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Logging;

    class DesiredPropertyUpdater : ITwinOperation
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DesiredPropertyUpdater));
        readonly IotHubServiceClient serviceClient;
        readonly ITwinTestResultHandler resultHandler;
        readonly TwinTestState twinTestState;
        int desiredPropertyUpdateCounter;

        public DesiredPropertyUpdater(IotHubServiceClient serviceClient, ITwinTestResultHandler resultHandler, TwinTestState twinTestState)
        {
            this.serviceClient = serviceClient;
            this.resultHandler = resultHandler;
            this.twinTestState = twinTestState;
            this.desiredPropertyUpdateCounter = twinTestState.DesiredPropertyUpdateCounter;
        }

        public async Task UpdateAsync()
        {
            try
            {
                string desiredPropertyUpdateValue = new string('1', Settings.Current.TwinUpdateSize); // dummy twin update can be any character

                var patch = new ClientTwin();
                string propertyKey = this.desiredPropertyUpdateCounter.ToString();
                patch.Properties.Desired[propertyKey] = desiredPropertyUpdateValue;

                ClientTwin newTwin = await this.serviceClient.Twins.UpdateAsync(Settings.Current.DeviceId, Settings.Current.TargetModuleId, patch, false, CancellationToken.None);
                this.twinTestState.TwinETag = newTwin.ETag.ToString();

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
