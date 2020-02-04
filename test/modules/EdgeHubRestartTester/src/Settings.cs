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
            TimeSpan sdkOperationTimeout,
            string serviceClientConnectionString,
            string deviceId,
            string reportingEndpointUrl,
            TimeSpan restartPeriod,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            bool directMethodEnabled,
            string directMethodName,
            string directMethodTargetModuleId,
            bool messageEnabled,
            string messageOutputEndpoint,
            TransportType transportType,
            string moduleId,
            string trackingId)
        {
            Preconditions.CheckRange(sdkOperationTimeout.Ticks, 0);
            Preconditions.CheckRange(restartPeriod.Ticks, 0);
            Preconditions.CheckRange(testStartDelay.Ticks, 0);
            Preconditions.CheckRange(testDuration.Ticks, 0);

            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.DirectMethodEnabled = directMethodEnabled;
            this.DirectMethodName = this.DirectMethodEnabled ? Preconditions.CheckNonWhiteSpace(directMethodName, nameof(directMethodName)) : string.Empty;
            this.DirectMethodTargetModuleId = this.DirectMethodEnabled ? Preconditions.CheckNonWhiteSpace(directMethodTargetModuleId, nameof(directMethodTargetModuleId)) : string.Empty;
            this.MessageEnabled = messageEnabled;
            this.MessageOutputEndpoint = this.MessageEnabled ? Preconditions.CheckNonWhiteSpace(messageOutputEndpoint, nameof(messageOutputEndpoint)) : string.Empty;
            this.TransportType = transportType;
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.ReportingEndpointUrl = new Uri(Preconditions.CheckNonWhiteSpace(reportingEndpointUrl, nameof(reportingEndpointUrl)));
            this.RestartPeriod = restartPeriod;
            this.SdkOperationTimeout = sdkOperationTimeout;
            this.IoTHubConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));

            if (!(this.DirectMethodEnabled || this.MessageEnabled))
            {
                throw new NotSupportedException("EdgeHubRestartTester requires at least one of the sending methods {DirectMethodEnabled, MessageEnabled} to be enabled to perform the EdgeHub restarting test.");
            }

            if (restartPeriod < sdkOperationTimeout)
            {
                throw new InvalidDataException("sdkOperationTimeout period must be less than restartInterval period.");
            }

            if (this.RestartPeriod.Ticks < TimeSpan.FromMinutes(1).Ticks)
            {
                throw new InvalidDataException("RestartPeriod period must be at least one minute");
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
                configuration.GetValue("sdkOperationTimeout", TimeSpan.FromMilliseconds(20)),
                configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING", string.Empty),
                configuration.GetValue<string>("IOTEDGE_DEVICEID", string.Empty),
                configuration.GetValue<string>("reportingEndpointUrl"),
                configuration.GetValue("restartPeriod", TimeSpan.FromMinutes(5)),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                configuration.GetValue("testDuration", TimeSpan.Zero),
                configuration.GetValue<bool>("directMethodEnabled", false),
                configuration.GetValue<string>("directMethodName", "HelloWorldMethod"),
                configuration.GetValue<string>("directMethodTargetModuleId", "DirectMethodReceiver"),
                configuration.GetValue<bool>("messageEnabled", false),
                configuration.GetValue("messageOutputEndpoint", "output1"),
                configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue("trackingId", string.Empty));
        }

        public string IoTHubConnectionString { get; }

        public string DeviceId { get; }

        public bool DirectMethodEnabled { get; }

        public string DirectMethodName { get; }

        public string DirectMethodTargetModuleId { get; }

        public bool MessageEnabled { get; }

        public string MessageOutputEndpoint { get; }

        public TransportType TransportType { get; }

        public string ModuleId { get; }

        public Uri ReportingEndpointUrl { get; }

        public TimeSpan RestartPeriod { get; }

        public TimeSpan SdkOperationTimeout { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan TestDuration { get; }

        public string TrackingId { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.DirectMethodEnabled), this.DirectMethodEnabled.ToString() },
                { nameof(this.DirectMethodName), this.DirectMethodName },
                { nameof(this.DirectMethodTargetModuleId), this.DirectMethodTargetModuleId },
                { nameof(this.MessageEnabled), this.MessageEnabled.ToString() },
                { nameof(this.MessageOutputEndpoint), this.MessageOutputEndpoint },
                { nameof(this.TransportType), this.TransportType.ToString() },
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.ReportingEndpointUrl), this.ReportingEndpointUrl.ToString() },
                { nameof(this.RestartPeriod), this.RestartPeriod.ToString() },
                { nameof(this.SdkOperationTimeout), this.SdkOperationTimeout.ToString() },
                { nameof(this.TestStartDelay), this.TestStartDelay.ToString() },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.TrackingId), this.TrackingId }
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
