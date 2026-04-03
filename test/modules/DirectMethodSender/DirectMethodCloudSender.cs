// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class DirectMethodCloudSender : DirectMethodSenderBase
    {
        readonly IotHubServiceClient serviceClient;

        DirectMethodCloudSender(
            IotHubServiceClient serviceClient,
            ILogger logger)
            : base(
                logger,
                Settings.Current.DeviceId,
                Settings.Current.TargetModuleId)
        {
            this.serviceClient = Preconditions.CheckNotNull(serviceClient, nameof(serviceClient));
        }

        public override ValueTask DisposeAsync()
        {
            this.serviceClient.Dispose();
            return default;
        }

        public static Task<DirectMethodCloudSender> CreateAsync(
            string connectionString,
            ILogger logger)
        {
            var serviceClient = new IotHubServiceClient(connectionString);
            return Task.FromResult(new DirectMethodCloudSender(
                serviceClient,
                logger));
        }

        internal override async Task<int> InvokeDeviceMethodAsync(
            string deviceId,
            string targetModuleId,
            string methodName,
            ulong directMethodCount,
            CancellationToken none)
        {
            var directMethodRequest = new DirectMethodServiceRequest(methodName);
            directMethodRequest.SetPayloadJson($"{{ \"Message\": \"Hello\", \"DirectMethodCount\": \"{directMethodCount.ToString()}\" }}");
            DirectMethodClientResponse result = await this.serviceClient.DirectMethods.InvokeAsync(deviceId, targetModuleId, directMethodRequest, CancellationToken.None);
            return result.Status;
        }
    }
}
