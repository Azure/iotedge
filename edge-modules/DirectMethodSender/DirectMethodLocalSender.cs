// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public sealed class DirectMethodLocalSender : DirectMethodSenderBase
    {
        ModuleClient moduleClient;

        private DirectMethodLocalSender(
            ModuleClient moduleClient,
            ILogger logger)
            : base(
                logger,
                Settings.Current.DeviceId,
                Settings.Current.TargetModuleId)
        {
            this.moduleClient = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
        }

        public override void Dispose() => this.moduleClient.Dispose();

        public static async Task<DirectMethodLocalSender> CreateAsync(
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

            return new DirectMethodLocalSender(
                moduleClient,
                logger);
        }

        internal override async Task<int> InvokeDeviceMethodAsync(string deviceId, string targetModuleId, CancellationToken none)
        {
            MethodRequest request = new MethodRequest("HelloWorldMethod", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));
            MethodResponse result = await this.moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);
            return result.Status;
        }
    }
}
