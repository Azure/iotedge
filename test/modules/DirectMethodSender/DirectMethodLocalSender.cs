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

    sealed class DirectMethodLocalSender : DirectMethodSenderBase
    {
        IotHubModuleClient moduleClient;

        private DirectMethodLocalSender(
            IotHubModuleClient moduleClient,
            ILogger logger)
            : base(
                logger,
                Settings.Current.DeviceId,
                Settings.Current.TargetModuleId)
        {
            this.moduleClient = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
        }

        public override async ValueTask DisposeAsync() => await this.moduleClient.DisposeAsync();

        public static async Task<DirectMethodLocalSender> CreateAsync(
            TransportType transportType,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy,
            RetryStrategy retryStrategy,
            ILogger logger)
        {
            // implicit OpenAsync()
            IotHubModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    transportType,
                    null,
                    transientErrorDetectionStrategy,
                    retryStrategy,
                    logger);

            return new DirectMethodLocalSender(
                moduleClient,
                logger);
        }

        internal override async Task<int> InvokeDeviceMethodAsync(
            string deviceId,
            string targetModuleId,
            string methodName,
            ulong directMethodCount,
            CancellationToken none)
        {
            string jsonPayload = $"{{ \"Message\": \"Hello\", \"DirectMethodCount\": \"{directMethodCount.ToString()}\" }}";
            EdgeModuleDirectMethodRequest request = new EdgeModuleDirectMethodRequest(methodName, Encoding.UTF8.GetBytes(jsonPayload));
            request.ResponseTimeoutInSeconds = 300;
            request.ConnectTimeoutInSeconds = 300;
            DirectMethodResponse result = await this.moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request, CancellationToken.None);
            return result.Status;
        }
    }
}
