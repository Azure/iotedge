// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    sealed class DirectMethodCloudSender : DirectMethodSenderBase
    {
        readonly ServiceClient serviceClient;

        DirectMethodCloudSender(
            ServiceClient serviceClient,
            ILogger logger)
            : base(
                logger,
                Settings.Current.DeviceId,
                Settings.Current.TargetModuleId)
        {
            this.serviceClient = Preconditions.CheckNotNull(serviceClient, nameof(serviceClient));
        }

        public override void Dispose() => this.serviceClient.Dispose();

        public static async Task<DirectMethodCloudSender> CreateAsync(
            string connectionString,
            TransportType transportType,
            ILogger logger)
        {
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString, transportType);
            await serviceClient.OpenAsync();
            return new DirectMethodCloudSender(
                serviceClient,
                logger);
        }

        internal override async Task<int> InvokeDeviceMethodAsync(
            string deviceId,
            string targetModuleId,
            string methodName,
            ulong directMethodCount,
            CancellationToken none)
        {
            // serviceClientTimeouts needs to be explicitly set to let IoTHub figure out where does the device is connected to the IoTHub server.
            // Otherwise, the IoTHub would immediately throw the DeviceNotFound exception if the target device re-connect to a different server than its original connection.
            TimeSpan serviceClientTimeouts = TimeSpan.FromSeconds(45);
            CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod(methodName).SetPayloadJson($"{{ \"Message\": \"Hello\", \"DirectMethodCount\": \"{directMethodCount.ToString()}\" }}");
            cloudToDeviceMethod.ConnectionTimeout = serviceClientTimeouts;
            cloudToDeviceMethod.ResponseTimeout = serviceClientTimeouts;
            CloudToDeviceMethodResult result = await this.serviceClient.InvokeDeviceMethodAsync(deviceId, targetModuleId, cloudToDeviceMethod, CancellationToken.None);
            return result.Status;
        }
    }
}
