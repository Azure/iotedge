// Copyright (c) Microsoft. All rights reserved.
namespace PlugAndPlayDevice
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
            TransportType transportType,
            TimeSpan testStartDelay)
        {
            Preconditions.CheckRange(testStartDelay.Ticks, 0);
            Preconditions.CheckArgument(Enum.IsDefined(typeof(TransportType), transportType));
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));

            this.IotHubConnectionString = Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
            this.DeviceId = deviceId + "-" + transportType.ToString() + "-leaf";
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.GatewayHostName = Preconditions.CheckNonWhiteSpace(gatewayHostName, nameof(gatewayHostName));
            this.WorkloadUri = Preconditions.CheckNonWhiteSpace(workloadUri, nameof(workloadUri));
            this.ApiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.ModuleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));
            this.IotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.TransportType = transportType;
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
                configuration.GetValue("transportType", TransportType.Amqp),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)));
        }

        internal Uri ReportingEndpointUrl { get; }

        public string IotHubConnectionString { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public string GatewayHostName { get; }

        public string WorkloadUri { get; }

        public string ApiVersion { get; }

        public string ModuleGenerationId { get; }

        public string IotHubHostName { get; }

        public TransportType TransportType { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType) },
                { nameof(this.ReportingEndpointUrl), this.ReportingEndpointUrl.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
