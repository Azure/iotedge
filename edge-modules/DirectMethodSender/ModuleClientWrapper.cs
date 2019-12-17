// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class ModuleClientWrapper : DirectMethodClientBase
    {
        ModuleClient moduleClient;

        private ModuleClientWrapper(
            ModuleClient moduleClient,
            ILogger logger)
            : base(logger)
        {
            this.moduleClient = moduleClient;
        }

        public override Task CloseAsync() => this.moduleClient.CloseAsync();

        public static async Task<ModuleClientWrapper> CreateAsync(
            TransportType transportType,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy,
            RetryStrategy retryStrategy,
            ILogger logger)
        {
            // implicit OpenAsync()
            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    transportType,
                    transientErrorDetectionStrategy,
                    retryStrategy,
                    logger);

            return new ModuleClientWrapper(
                moduleClient,
                logger);
        }

        internal override async Task<int> InvokeDeviceMethodAsync(string deviceId, string targetModuleId, CancellationToken none)
        {
            MethodRequest request = new MethodRequest("HelloWorldMethod", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));
            MethodResponse result = await this.moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);
            return result.Status;
        }

        public override Task OpenAsync() => this.moduleClient.OpenAsync();
    }
}
