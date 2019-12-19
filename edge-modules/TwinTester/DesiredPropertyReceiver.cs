// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class DesiredPropertyReceiver : ITwinOperation
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(DesiredPropertyReceiver));
        readonly RegistryManager registryManager;
        readonly ModuleClient moduleClient;
        readonly ITwinTestResultHandler resultHandler;

        public DesiredPropertyReceiver(RegistryManager registryManager, ModuleClient moduleClient, ITwinTestResultHandler resultHandler)
        {
            this.registryManager = registryManager;
            this.moduleClient = moduleClient;
            this.resultHandler = resultHandler;
        }

        public Task UpdateAsync()
        {
            return this.moduleClient.SetDesiredPropertyUpdateCallbackAsync(this.OnDesiredPropertyUpdateAsync, null);
        }

        async Task OnDesiredPropertyUpdateAsync(TwinCollection desiredProperties, object userContext)
        {
            Logger.LogDebug($"Received desired property {desiredProperties.ToString()}");
            await this.resultHandler.HandleDesiredPropertyReceivedAsync(desiredProperties);
        }
    }
}
