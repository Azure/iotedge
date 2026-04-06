// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
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
        IotHubDeviceClient deviceClient;

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

        public async ValueTask DisposeAsync()
        {
            if (this.deviceClient != null)
            {
                await this.deviceClient.DisposeAsync();
            }
        }

        internal async Task ReportTestResult(IncomingMessage message)
        {
            string trackingId = message.Properties[TestConstants.Message.TrackingIdPropertyName];
            string batchId = message.Properties[TestConstants.Message.BatchIdPropertyName];
            string sequenceNumber = message.Properties[TestConstants.Message.SequenceNumberPropertyName];
            var testResultReceived = new MessageTestResult(this.moduleId + ".receive", DateTime.UtcNow)
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
            // TODO: Update for v2 SDK - ITransportSettings not available in v2
            // ITransportSettings transportSettings = ((Protocol)Enum.Parse(typeof(Protocol), this.transportType.ToString())).ToTransportSettings();
            // OsPlatform.Current.InstallCaCertificates(certs, transportSettings);
            IotHubServiceClient serviceClient = null;

            try
            {
                serviceClient = new IotHubServiceClient(this.iotHubConnectionString);
                var edgeDevice = await serviceClient.Devices.GetAsync(this.edgeDeviceId);
                var leafDevice = new Device(this.deviceId);
                leafDevice.Scope = edgeDevice.Scope;
                Device device = await serviceClient.Devices.CreateAsync(leafDevice, ct);
                string deviceConnectionString = $"HostName={this.iotHubHostName};DeviceId={this.deviceId};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={this.gatewayHostName}";
                var clientOptions = new IotHubClientOptions(new IotHubClientAmqpSettings());
                this.deviceClient = new IotHubDeviceClient(deviceConnectionString, clientOptions);

                var retryStrategy = new Incremental(15, RetryStrategy.DefaultRetryInterval, RetryStrategy.DefaultRetryIncrement);
                var retryPolicy = new RetryPolicy(new FailingConnectionErrorDetectionStrategy(), retryStrategy);
                await retryPolicy.ExecuteAsync(
                    async () =>
                {
                    try
                    {
                        await this.deviceClient.OpenAsync(ct);
                    }
                    catch (Exception e)
                    {
                        this.logger.LogInformation("Device client encountered exception on connection attempt: {0}", e);
                        throw;
                    }
                }, ct);

                // TODO: Update for v2 SDK - polling ReceiveMessageAsync replaced with callback pattern
                this.logger.LogInformation("Ready to receive messages via callback");
                await this.deviceClient.SetIncomingMessageCallbackAsync(async (IncomingMessage message) =>
                {
                    try
                    {
                        if (message == null)
                        {
                            this.logger.LogWarning("Received message is null");
                            return MessageAcknowledgement.Complete;
                        }

                        if (!message.Properties.ContainsKey(TestConstants.Message.SequenceNumberPropertyName) ||
                            !message.Properties.ContainsKey(TestConstants.Message.BatchIdPropertyName) ||
                            !message.Properties.ContainsKey(TestConstants.Message.TrackingIdPropertyName))
                        {
                            string propertyKeys = string.Join(",", message.Properties.Keys);
                            this.logger.LogWarning($"Received message doesn't contain required key. property keys: {propertyKeys}.");
                            return MessageAcknowledgement.Complete;
                        }

                        this.logger.LogInformation($"Message received. " +
                            $"Sequence Number: {message.Properties[TestConstants.Message.SequenceNumberPropertyName]}, " +
                            $"batchId: {message.Properties[TestConstants.Message.BatchIdPropertyName]}, " +
                            $"trackingId: {message.Properties[TestConstants.Message.TrackingIdPropertyName]}.");
                        await this.ReportTestResult(message);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Error occurred while receiving message.");
                    }

                    return MessageAcknowledgement.Complete;
                });

                await Task.Delay(Timeout.Infinite, ct);
            }
            finally
            {
                if (serviceClient != null)
                {
                    serviceClient.Dispose();
                }
            }
        }
    }

    // This error detection strategy is intended for SDK clients connecting
    // to EdgeHub encountering auth issue related to dotnet 6.
    //
    // Can be removed when the below is fixed:
    // (InvalidOperationException) - Devices SDK Issue: No authenticated context (https://github.com/Azure/azure-iot-sdk-csharp/issues/2353)
    class FailingConnectionErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        public bool IsTransient(Exception ex)
        {
            return ex is InvalidOperationException && ex.Message.Contains("This operation is only allowed using a successfully authenticated context.");
        }
    }
}
