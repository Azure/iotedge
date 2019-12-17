// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    public class DirectMethodCloudSender : DirectMethodSenderClient
    {
        readonly ServiceClient serviceClient;

        private DirectMethodCloudSender(
            ServiceClient serviceClient,
            ILogger logger)
            : base(logger)
        {
            this.serviceClient = serviceClient;
        }

        public override Task CloseAsync() => this.serviceClient.CloseAsync();

        public static DirectMethodCloudSender Create(
            string connectionString,
            TransportType transportType,
            ILogger logger)
        {
            ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString, transportType);
            return new DirectMethodCloudSender(
                serviceClient,
                logger);
        }

        internal override async Task<int> InvokeDeviceMethodAsync(string deviceId, string targetModuleId, CancellationToken none)
        {
            CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod("HelloWorldMethod").SetPayloadJson("{ \"Message\": \"Hello\" }");
            CloudToDeviceMethodResult result = await this.serviceClient.InvokeDeviceMethodAsync(deviceId, targetModuleId, cloudToDeviceMethod, CancellationToken.None);
            return result.Status;
        }

        public override Task OpenAsync() => this.serviceClient.OpenAsync();
    }
}
