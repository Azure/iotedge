// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    sealed class CloudToDeviceMessageReceiver : ICloudToDeviceMessageTester
    {
        readonly ILogger logger;
        readonly string iotHubConnectionString;
        readonly string deviceId;
        readonly string edgeDeviceId;
        readonly string moduleId;
        readonly TransportType transportType;
        readonly TestResultReportingClient testResultReportingClient;
        readonly string gatewayHostName;
        readonly string workloadUri;
        readonly string apiVersion;
        readonly string workloadClientApiVersion = "2019-01-30";
        readonly string moduleGenerationId;
        readonly string iotHubHostName;
        DeviceClient deviceClient;

        internal CloudToDeviceMessageReceiver(
            ILogger logger,
            C2DTestSharedSettings sharedMetadata,
            C2DTestReceiverSettings receiverMetadata,
            TestResultReportingClient testResultReportingClient)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.iotHubConnectionString = Preconditions.CheckNonWhiteSpace(sharedMetadata.IotHubConnectionString, nameof(sharedMetadata.IotHubConnectionString));
            this.deviceId = Preconditions.CheckNonWhiteSpace(sharedMetadata.DeviceId, nameof(sharedMetadata.DeviceId));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(receiverMetadata.EdgeDeviceId, nameof(receiverMetadata.EdgeDeviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(sharedMetadata.ModuleId, nameof(sharedMetadata.ModuleId));
            this.transportType = receiverMetadata.TransportType;
            this.gatewayHostName = Preconditions.CheckNonWhiteSpace(receiverMetadata.GatewayHostName, nameof(receiverMetadata.GatewayHostName));
            this.workloadUri = Preconditions.CheckNonWhiteSpace(receiverMetadata.WorkloadUri, nameof(receiverMetadata.WorkloadUri));
            this.apiVersion = Preconditions.CheckNonWhiteSpace(receiverMetadata.ApiVersion, nameof(receiverMetadata.ApiVersion));
            this.moduleGenerationId = Preconditions.CheckNonWhiteSpace(receiverMetadata.ModuleGenerationId, nameof(receiverMetadata.ModuleGenerationId));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(receiverMetadata.IotHubHostName, nameof(receiverMetadata.IotHubHostName));
            this.testResultReportingClient = Preconditions.CheckNotNull(testResultReportingClient, nameof(testResultReportingClient));
        }

        public void Dispose() => this.deviceClient?.Dispose();

        internal async Task ReportTestResult(Message message)
        {
            string trackingId = message.Properties[TestConstants.Message.TrackingIdPropertyName];
            string batchId = message.Properties[TestConstants.Message.BatchIdPropertyName];
            string sequenceNumber = message.Properties[TestConstants.Message.SequenceNumberPropertyName];
            var testResultReceived = new MessageTestResult(this.moduleId + ".send", DateTime.UtcNow)
            {
                TrackingId = trackingId,
                BatchId = batchId,
                SequenceNumber = sequenceNumber
            };
            await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, this.logger, testResultReceived);
        }

        public async Task StartAsync(CancellationToken ct)
        {
            // TODO: You cannot install certificate on Windows by script - we need to implement certificate verification callback handler.
            IEnumerable<X509Certificate2> certs = await CertificateHelper.GetTrustBundleFromEdgelet(new Uri(this.workloadUri), this.apiVersion, this.workloadClientApiVersion, this.moduleId, this.moduleGenerationId);
            ITransportSettings transportSettings = ((Protocol)Enum.Parse(typeof(Protocol), this.transportType.ToString())).ToTransportSettings();
            OsPlatform.Current.InstallCaCertificates(certs, transportSettings);
            Microsoft.Azure.Devices.RegistryManager registryManager = null;

            Guid batchId = Guid.NewGuid();
            this.logger.LogInformation($"Batch Id={batchId}");
            Guid trackingId = Guid.NewGuid();
            this.logger.LogInformation($"Tracking Id={trackingId}");

            try
            {
                registryManager = Microsoft.Azure.Devices.RegistryManager.CreateFromConnectionString(this.iotHubConnectionString);
                var edgeDevice = await registryManager.GetDeviceAsync(this.edgeDeviceId);
                var leafDevice = new Microsoft.Azure.Devices.Device(this.deviceId);
                leafDevice.Scope = edgeDevice.Scope;
                Microsoft.Azure.Devices.Device device = await registryManager.AddDeviceAsync(leafDevice, ct);
                string deviceConnectionString = $"HostName={this.iotHubHostName};DeviceId={this.deviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={this.gatewayHostName}";
                this.deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, new ITransportSettings[] { transportSettings });
                await this.deviceClient.OpenAsync();
                int messageCount = 0;

                int delay = new Random().Next((int)TimeSpan.FromMinutes(5).TotalMilliseconds, (int)TimeSpan.FromMinutes(50).TotalMilliseconds);
                this.logger.LogInformation($"Leaf device update after {TimeSpan.FromMilliseconds(delay).TotalMinutes}");

                while (!ct.IsCancellationRequested)
                {
                    this.logger.LogInformation("Ready to receive message");
                    try
                    {
                        messageCount++;
                        var message = new Message(Encoding.ASCII.GetBytes("Leaf message."));
                        message.Properties.Add(TestConstants.Message.SequenceNumberPropertyName, messageCount.ToString());
                        message.Properties.Add(TestConstants.Message.BatchIdPropertyName, batchId.ToString());
                        message.Properties.Add(TestConstants.Message.TrackingIdPropertyName, trackingId.ToString());
                        await this.deviceClient.SendEventAsync(message);

                        this.logger.LogInformation($"Message sent. " +
                            $"Sequence Number: {message.Properties[TestConstants.Message.SequenceNumberPropertyName]}, " +
                            $"batchId: {message.Properties[TestConstants.Message.BatchIdPropertyName]}, " +
                            $"trackingId: {message.Properties[TestConstants.Message.TrackingIdPropertyName]}, ");
                        await this.ReportTestResult(message);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Error occurred while receiving message.");
                    }
                }
            }
            finally
            {
                registryManager?.Dispose();
            }
        }
    }
}
