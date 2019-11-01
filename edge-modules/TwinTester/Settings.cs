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

    // TODO: remove
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
                    configuration.GetValue<double>("jitterFactor", 0),
                    configuration.GetValue<TimeSpan>("twinUpdateFrequency", TimeSpan.FromMilliseconds(500)),
                    configuration.GetValue<TransportType>("transportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue<string>("analyzerUrl", "http://analyzer:15000"),
                    configuration.GetValue<string>("serviceClientConnectionString", string.Empty),
                    configuration.GetValue<string>("StoragePath", string.Empty),
                    configuration.GetValue<bool>("StorageOptimizeForPerformance", true));
            });

        Settings(
            string deviceId,
            string moduleId,
            double jitterFactor,
            TimeSpan twinUpdateFrequency,
            TransportType transportType,
            string analyzerUrl,
            string serviceClientConnectionString,
            string storagePath,
            bool storageOptimizeForPerformance)
        {
            this.DeviceId = deviceId;
            this.ModuleId = moduleId;
            this.TwinUpdateFrequency = twinUpdateFrequency;
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
        public TimeSpan TwinUpdateFrequency { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }
        public string AnalyzerUrl { get; }
        public string ServiceClientConnectionString { get; }
        public string StoragePath { get; }
        public bool StorageOptimizeForPerformance { get; }

        // TODO: change approach to not log connection string
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
