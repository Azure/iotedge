// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
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
                    configuration.GetValue<string>("IOTEDGE_DEVICEID", string.Empty),
                    configuration.GetValue<string>("IOTEDGE_MODULEID", string.Empty),
                    configuration.GetValue<double>("JitterFactor", 0),
                    configuration.GetValue<int>("TwinUpdateCharCount", 1),
                    configuration.GetValue<TimeSpan>("TwinUpdateFrequency", TimeSpan.FromSeconds(10)),
                    configuration.GetValue<TimeSpan>("TwinUpdateFailureThreshold", TimeSpan.FromMinutes(1)),
                    configuration.GetValue<TransportType>("TransportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue<string>("AnalyzerUrl", "http://analyzer:15000"),
                    configuration.GetValue<string>("ServiceClientConnectionString"),
                    configuration.GetValue<string>("StoragePath"),
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
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.TwinUpdateCharCount = Preconditions.CheckRange(twinUpdateCharCount, 0);
            this.TwinUpdateFrequency = Preconditions.CheckNotNull(twinUpdateFrequency);
            this.TwinUpdateFailureThreshold = Preconditions.CheckNotNull(twinUpdateFailureThreshold);
            this.TransportType = Preconditions.CheckNotNull(transportType);
            this.AnalyzerUrl = Preconditions.CheckNonWhiteSpace(analyzerUrl, nameof(analyzerUrl));
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.StoragePath = storagePath;
            this.StorageOptimizeForPerformance = Preconditions.CheckNotNull(storageOptimizeForPerformance);
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
