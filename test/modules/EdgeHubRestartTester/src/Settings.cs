// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class Settings
    {
        internal static Settings Current = Create();
        List<EdgeHubConnectorsConfig> connectorConfig;
        bool isConnectorConfigReady = false;

        Settings(
            TimeSpan sdkOperationTimeout,
            string serviceClientConnectionString,
            string deviceId,
            string reportingEndpointUrl,
            TimeSpan restartPeriod,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            string directMethodName,
            string moduleId,
            string trackingId)
        {
            Preconditions.CheckRange(sdkOperationTimeout.Ticks, 0);
            Preconditions.CheckRange(restartPeriod.Ticks, 0);
            Preconditions.CheckRange(testStartDelay.Ticks, 0);
            Preconditions.CheckRange(testDuration.Ticks, 0);

            this.connectorConfig = new List<EdgeHubConnectorsConfig>();
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.DirectMethodName = directMethodName;
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.ReportingEndpointUrl = new Uri(Preconditions.CheckNonWhiteSpace(reportingEndpointUrl, nameof(reportingEndpointUrl)));
            this.RestartPeriod = restartPeriod;
            this.SdkOperationTimeout = sdkOperationTimeout;
            this.IoTHubConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));

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
                configuration.GetValue<string>("directMethodName", "HelloWorldMethod"),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue("trackingId", string.Empty));
        }

        public string IoTHubConnectionString { get; }

        public string DeviceId { get; }

        public string DirectMethodName { get; private set; }

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
                { nameof(this.DirectMethodName), this.DirectMethodName },
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

        internal async Task<List<EdgeHubConnectorsConfig>> GetConnectorConfigAsync()
        {
            if (!this.isConnectorConfigReady)
            {
                RegistryManager rm = RegistryManager.CreateFromConnectionString(this.IoTHubConnectionString);
                Twin moduleTwin = await rm.GetTwinAsync(this.DeviceId, this.ModuleId);
                string connectorConfigJson = moduleTwin.Properties.Desired["edgeHubConnectorConfig"].ToString();

                JObject edgeHubConnectorConfig = JObject.Parse(connectorConfigJson);

                foreach (JToken eachConfig in edgeHubConnectorConfig.Children())
                {
                    this.connectorConfig.Add(JsonConvert.DeserializeObject<EdgeHubConnectorsConfig>(((JProperty)eachConfig).Value.ToString()));
                }

                foreach (EdgeHubConnectorsConfig eachConfig in this.connectorConfig)
                {
                    if (string.IsNullOrWhiteSpace(eachConfig.DirectMethodTargetModuleId) && string.IsNullOrWhiteSpace(eachConfig.MessageOutputEndpoint))
                    {
                        throw new NotSupportedException("EdgeHubRestartTester requires at least one of the sending methods to be enabled to perform the EdgeHub restarting test.");
                    }

                    if (!string.IsNullOrWhiteSpace(eachConfig.DirectMethodTargetModuleId))
                    {
                        Preconditions.CheckNonWhiteSpace(this.DirectMethodName, nameof(this.DirectMethodName));
                    }
                }

                this.isConnectorConfigReady = true;
            }

            return this.connectorConfig;
        }
    }
}
