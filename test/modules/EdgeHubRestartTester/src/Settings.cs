// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
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
            string serviceClientConnectionString,
            string deviceId,
            string reportingEndpointUrl,
            int restartIntervalInMins,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            string directMethodName,
            string directMethodTargetModuleId,
            string messageOutputEndpoint,
            TransportType messageTransportType,
            string trackingId
            )
        {
            Preconditions.CheckRange(testStartDelay.Ticks, 0);
            Preconditions.CheckRange(testDuration.Ticks, 0);

            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.DirectMethodName = Preconditions.CheckNonWhiteSpace(directMethodName, nameof(directMethodName));
            this.DirectMethodTargetModuleId = Preconditions.CheckNonWhiteSpace(directMethodTargetModuleId, nameof(directMethodTargetModuleId));
            this.MessageOutputEndpoint = Preconditions.CheckNonWhiteSpace(messageOutputEndpoint, nameof(messageOutputEndpoint));
            this.MessageTransportType = messageTransportType;
            this.ReportingEndpointUrl = new Uri(Preconditions.CheckNonWhiteSpace(reportingEndpointUrl, nameof(reportingEndpointUrl)));
            this.RestartIntervalInMins = Preconditions.CheckRange(restartIntervalInMins, 0);
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
            this.TrackingId = trackingId;
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return new Settings(
                configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING", string.Empty),
                configuration.GetValue<string>("IOTEDGE_DEVICEID", string.Empty),
                configuration.GetValue<string>("reportingEndpointUrl"),
                configuration.GetValue<int>("restartIntervalInMins", 5),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                configuration.GetValue("testDuration", TimeSpan.Zero),
                configuration.GetValue<string>("directMethodName", "HelloWorldMethod"),
                configuration.GetValue<string>("directMethodTargetModuleId", "DirectMethodReceiver"),
                configuration.GetValue("messageOutputEndpoint", "output1"),
                configuration.GetValue("messageTransportType", TransportType.Amqp_Tcp_Only),
                configuration.GetValue("trackingId", string.Empty));
        }

        public string ServiceClientConnectionString { get; }

        public string DeviceId { get; }

        public string DirectMethodName { get; }

        public string DirectMethodTargetModuleId { get; }

        public string MessageOutputEndpoint { get; }

        public TransportType MessageTransportType { get; }

        public Uri ReportingEndpointUrl { get; }

        public int RestartIntervalInMins { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan TestDuration { get; }

        public string TrackingId { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.DirectMethodName), this.DirectMethodName.ToString() },
                { nameof(this.DirectMethodTargetModuleId), this.DirectMethodTargetModuleId.ToString() },
                { nameof(this.MessageTransportType), this.MessageTransportType.ToString() },
                { nameof(this.ReportingEndpointUrl), this.ReportingEndpointUrl.ToString() },
                { nameof(this.RestartIntervalInMins), this.RestartIntervalInMins.ToString() },
                { nameof(this.TestStartDelay), this.TestStartDelay.ToString() },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.TrackingId), this.TrackingId.ToString() }
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
