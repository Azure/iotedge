// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodCloudSender
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Settings
    {
        static readonly Lazy<Settings> DefaultSettings = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                return new Settings(
                    configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                    configuration.GetValue<string>("TargetModuleId", "DirectMethodReceiver"),
                    configuration.GetValue<TransportType>("TransportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue<TimeSpan>("DirectMethodDelay", TimeSpan.FromSeconds(5)),
                    configuration.GetValue<Uri>("AnalyzerUrl", new Uri("http://analyzer:15000")),
                    configuration.GetValue<string>("ServiceClientConnectionString"));
            });

        Settings(
            string deviceId,
            string targetModuleId,
            TransportType transportType,
            TimeSpan directMethodDelay,
            Uri analyzerUrl,
            string serviceClientConnectionString)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.TargetModuleId = Preconditions.CheckNonWhiteSpace(targetModuleId, nameof(targetModuleId));
            this.TransportType = Preconditions.CheckNotNull(transportType, nameof(transportType));
            this.DirectMethodDelay = Preconditions.CheckNotNull(directMethodDelay);
            this.AnalyzerUrl = Preconditions.CheckNotNull(analyzerUrl);
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
        }

        public static Settings Current => DefaultSettings.Value;

        public string DeviceId { get; }

        public string TargetModuleId { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }

        public TimeSpan DirectMethodDelay { get; }

        public Uri AnalyzerUrl { get; }

        public string ServiceClientConnectionString { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
