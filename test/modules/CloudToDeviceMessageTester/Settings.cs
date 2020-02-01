// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Settings
    {
        internal static Settings Current = Create();

        Settings(
            string deviceId,
            string iotHubConnectionString,
            string moduleId,
            string gatewayHostName,
            string workloadUri,
            string apiVersion,
            string moduleGenerationId,
            string iotHubHostName,
            CloudToDeviceMessageTesterMode testMode,
            string trackingId,
            TransportType transportType,
            TimeSpan messageDelay,
            Uri reportingEndpointUrl,
            TimeSpan testDuration,
            TimeSpan testStartDelay)
        {
            Preconditions.CheckRange(testDuration.Ticks, 0);
            Preconditions.CheckRange(testStartDelay.Ticks, 0);
            Preconditions.CheckArgument(Enum.IsDefined(typeof(TransportType), transportType));
            Preconditions.CheckArgument(Enum.IsDefined(typeof(CloudToDeviceMessageTesterMode), testMode));
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.TestMode = testMode;
            this.ReportingEndpointUrl = Preconditions.CheckNotNull(reportingEndpointUrl, nameof(reportingEndpointUrl));

            this.SharedMetadata = new C2DTestSharedSettings()
            {
                IotHubConnectionString = Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString)),
                DeviceId = deviceId + "-" + transportType.ToString() + "-leaf",
                ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)),
            };

            if (testMode == CloudToDeviceMessageTesterMode.Receiver)
            {
                this.ReceiverMetadata = new C2DTestReceiverSettings()
                {
                    GatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName)),
                    WorkloadUri = Preconditions.CheckNonWhiteSpace(workloadUri, nameof(workloadUri)),
                    ApiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion)),
                    ModuleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId)),
                    IotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName)),
                    TransportType = transportType
                };
            }
            else
            {
                this.SenderMetadata = new C2DTestSenderSettings()
                {
                    TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId)),
                    MessageDelay = messageDelay,
                    TestStartDelay = testStartDelay,
                    TestDuration = testDuration
                };
            }
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return new Settings(
                configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING"),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue<string>("IOTEDGE_GATEWAYHOSTNAME"),
                configuration.GetValue<string>("IOTEDGE_WORKLOADURI"),
                configuration.GetValue<string>("IOTEDGE_APIVERSION"),
                configuration.GetValue<string>("IOTEDGE_MODULEGENERATIONID"),
                configuration.GetValue<string>("IOTEDGE_IOTHUBHOSTNAME"),
                configuration.GetValue("C2DMESSAGE_TESTER_MODE", CloudToDeviceMessageTesterMode.Receiver),
                configuration.GetValue<string>("trackingId"),
                configuration.GetValue("TransportType", TransportType.Amqp),
                configuration.GetValue("MessageDelay", TimeSpan.FromSeconds(5)),
                configuration.GetValue<Uri>("ReportingEndpointUrl"),
                configuration.GetValue("testDuration", TimeSpan.Zero),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)));
        }

        internal C2DTestSharedSettings SharedMetadata { get; }

        internal C2DTestReceiverSettings ReceiverMetadata { get; }

        internal C2DTestSenderSettings SenderMetadata { get; }

        internal CloudToDeviceMessageTesterMode TestMode { get; }

        internal Uri ReportingEndpointUrl { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.SharedMetadata.ModuleId), this.SharedMetadata.ModuleId },
                { nameof(this.SharedMetadata.DeviceId), this.SharedMetadata.DeviceId },
                { nameof(this.TestMode), this.TestMode.ToString() },
                { nameof(this.SenderMetadata.TestDuration), this.SenderMetadata.TestDuration.ToString() },
                { nameof(this.SenderMetadata.TestStartDelay), this.SenderMetadata.TestStartDelay.ToString() },
                { nameof(this.SenderMetadata.TrackingId), this.SenderMetadata.TrackingId },
                { nameof(this.ReceiverMetadata.TransportType), Enum.GetName(typeof(TransportType), this.ReceiverMetadata.TransportType) },
                { nameof(this.SenderMetadata.MessageDelay), this.SenderMetadata.MessageDelay.ToString() },
                { nameof(this.ReportingEndpointUrl), this.ReportingEndpointUrl.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
