// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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
            TransportType messageTransportType,
            string trackingId
            )
        {
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.MessageTransportType = messageTransportType;
            this.ReportingEndpointUrl = Uri(Preconditions.CheckNonWhiteSpace(reportingEndpointUrl, nameof(reportingEndpointUrl)));
            this.RestartIntervalInMins = Preconditions.CheckRange(restartIntervalInMins, 0);
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
                configuration.GetValue<string>("ReportingEndpointUrl"),
                configuration.GetValue<int>("RestartIntervalInMins", 5),
                configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                configuration.GetValue("trackingId", string.Empty));
        }

        public string ServiceClientConnectionString { get; }

        public string DeviceId { get; }

        public TransportType MessageTransportType { get; }

        public Uri ReportingEndpointUrl { get; }

        public int RestartIntervalInMins { get; }

        public string TrackingId { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.ReportingEndpointUrl), this.ReportingEndpointUrl.ToString() },
                { nameof(this.RestartIntervalInMins), this.RestartIntervalInMins.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
