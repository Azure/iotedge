// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Client;
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
                    configuration.GetValue<string>("IOTEDGE_DEVICEID", string.Empty),
                    configuration.GetValue<string>("IOTEDGE_MODULEID", string.Empty),
                    configuration.GetValue<double>("JitterFactor", 0),
                    configuration.GetValue<int>("TwinUpdateCharCount", 1),
                    configuration.GetValue<TimeSpan>("TwinUpdateFrequency", TimeSpan.FromMilliseconds(500)),
                    configuration.GetValue<TimeSpan>("TwinUpdateFailureThreshold", TimeSpan.FromMinutes(1)), // TODO: tune
                    configuration.GetValue<TransportType>("TransportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue<string>("AnalyzerUrl", "http://analyzer:15000"),
                    configuration.GetValue<string>("ServiceClientConnectionString", string.Empty),
                    configuration.GetValue<string>("StoragePath", string.Empty),
                    configuration.GetValue<bool>("StorageOptimizeForPerformance", true));
            });

        Settings(
            string deviceId,
            string moduleId,
            double jitterFactor,
            int twinUpdateCharCount,
            TimeSpan twinUpdateFrequency,
            TimeSpan twinUpdateFailureThreshold,
            TransportType transportType,
            string analyzerUrl,
            string serviceClientConnectionString,
            string storagePath,
            bool storageOptimizeForPerformance)
        {
            this.DeviceId = deviceId;
            this.ModuleId = moduleId;
            this.TwinUpdateCharCount = twinUpdateCharCount;
            this.TwinUpdateFrequency = twinUpdateFrequency;
            this.TwinUpdateFailureThreshold = twinUpdateFailureThreshold;
            this.TransportType = transportType;
            this.AnalyzerUrl = analyzerUrl;
            this.ServiceClientConnectionString = serviceClientConnectionString;
            this.StoragePath = storagePath;
            this.StorageOptimizeForPerformance = storageOptimizeForPerformance;
        }

        public static Settings Current => DefaultSettings.Value;

        public string DeviceId { get; }
        public string ModuleId { get; }
        public double JitterFactor { get; }
        public int TwinUpdateCharCount { get; }
        public TimeSpan TwinUpdateFrequency { get; }
        public TimeSpan TwinUpdateFailureThreshold { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }
        public string AnalyzerUrl { get; }
        [JsonIgnore]
        public string ServiceClientConnectionString { get; }
        public string StoragePath { get; }
        public bool StorageOptimizeForPerformance { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
