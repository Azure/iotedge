// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using TransportType = Microsoft.Azure.Devices.TransportType;

    class Settings
    {
        internal static Settings Current = Create();

        Settings(
            string deviceId,
            string iotHubConnectionString,
            string moduleId,
            string gatewayHostName,
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

            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.IoTHubConnectionString = Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.GatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            this.TestMode = testMode;
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.TransportType = transportType;
            this.MessageDelay = messageDelay;
            this.ReportingEndpointUrl = Preconditions.CheckNotNull(reportingEndpointUrl, nameof(reportingEndpointUrl));
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
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
                configuration.GetValue("CLOUD_TO_DEVICE_MESSAGE_TESTER_MODE", CloudToDeviceMessageTesterMode.Receiver),
                configuration.GetValue<string>("trackingId"),
                configuration.GetValue("TransportType", TransportType.Amqp),
                configuration.GetValue("MessageDelay", TimeSpan.FromSeconds(5)),
                configuration.GetValue<Uri>("ReportingEndpointUrl"),
                configuration.GetValue("testDuration", TimeSpan.Zero),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)));
        }

        internal string DeviceId { get; }

        internal string IoTHubConnectionString { get; }

        internal string ModuleId { get; }

        internal string GatewayHostName { get; }

        internal CloudToDeviceMessageTesterMode TestMode { get; }

        internal string TrackingId { get; }

        internal TransportType TransportType { get; }

        internal TimeSpan MessageDelay { get; }

        internal Uri ReportingEndpointUrl { get; }

        internal TimeSpan TestDuration { get; }

        internal TimeSpan TestStartDelay { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.TestMode), this.TestMode.ToString() },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.TestStartDelay), this.TestStartDelay.ToString() },
                { nameof(this.TrackingId), this.TrackingId },
                { nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType) },
                { nameof(this.MessageDelay), this.MessageDelay.ToString() },
                { nameof(this.ReportingEndpointUrl), this.ReportingEndpointUrl.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
