// Copyright (c) Microsoft. All rights reserved.
namespace CloudMessageSender
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
            TransportType transportType,
            TimeSpan messageDelay,
            Uri reportingEndpointUrl,
            string serviceClientConnectionString,
            string moduleId,
            TimeSpan testDuration,
            TimeSpan testStartDelay,
            string trackingId)
        {
            Preconditions.CheckRange(testDuration.Ticks, 0);
            Preconditions.CheckRange(testStartDelay.Ticks, 0);

            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Preconditions.CheckArgument(TransportType.IsDefined(typeof(TransportType), transportType));
            this.TransportType = transportType;
            this.MessageDelay = messageDelay;
            this.ReportingEndpointUrl = reportingEndpointUrl;
            this.ServiceClientConnectionString = serviceClientConnectionString;
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
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
                configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                configuration.GetValue("TransportType", TransportType.Amqp),
                configuration.GetValue("MessageDelay", TimeSpan.FromSeconds(5)),
                configuration.GetValue<Uri>("ReportingEndpointUrl"),
                configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING"),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue("testDuration", TimeSpan.Zero),
                configuration.GetValue("testStartDelay", TimeSpan.Zero),
                configuration.GetValue<string>("trackingId"));
        }

        internal string DeviceId { get; }

        internal TransportType TransportType { get; }

        internal TimeSpan MessageDelay { get; }

        internal string ServiceClientConnectionString { get; }

        internal Uri ReportingEndpointUrl { get; }

        internal string ModuleId { get; }

        internal TimeSpan TestDuration { get; }

        internal TimeSpan TestStartDelay { get; }

        internal string TrackingId { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.DeviceId), this.DeviceId },
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
