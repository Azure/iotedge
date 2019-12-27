// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

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

                string moduleId = configuration.GetValue<string>("TargetModuleId");
                moduleId = string.IsNullOrWhiteSpace(moduleId) ? configuration.GetValue<string>("IOTEDGE_MODULEID", string.Empty) : moduleId;

                return new Settings(
                    configuration.GetValue<string>("IOTEDGE_DEVICEID", string.Empty),
                    moduleId,
                    configuration.GetValue<int>("TwinUpdateSize", 1),
                    configuration.GetValue<TimeSpan>("TwinUpdateFrequency", TimeSpan.FromSeconds(10)),
                    configuration.GetValue<TimeSpan>("TwinUpdateFailureThreshold", TimeSpan.FromMinutes(1)),
                    configuration.GetValue<TransportType>("TransportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue<Uri>("AnalyzerUrl", new Uri("http://analyzer:15000")),
                    configuration.GetValue<string>("ServiceClientConnectionString"),
                    configuration.GetValue<string>("StoragePath"),
                    configuration.GetValue<bool>("StorageOptimizeForPerformance", true),
                    configuration.GetValue<TestMode>("TestMode", TestMode.TwinAllOperations));
            });

        Settings(
            string deviceId,
            string moduleId,
            int twinUpdateSize,
            TimeSpan twinUpdateFrequency,
            TimeSpan twinUpdateFailureThreshold,
            TransportType transportType,
            Uri analyzerUrl,
            string serviceClientConnectionString,
            string storagePath,
            bool storageOptimizeForPerformance,
            TestMode testMode)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.TwinUpdateSize = Preconditions.CheckRange(twinUpdateSize, 1);
            this.TwinUpdateFrequency = Preconditions.CheckNotNull(twinUpdateFrequency);
            this.TwinUpdateFailureThreshold = Preconditions.CheckNotNull(twinUpdateFailureThreshold);
            this.TransportType = Preconditions.CheckNotNull(transportType);
            this.AnalyzerUrl = Preconditions.CheckNotNull(analyzerUrl, nameof(analyzerUrl));
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.StoragePath = Preconditions.CheckNotNull(storagePath);
            this.StorageOptimizeForPerformance = Preconditions.CheckNotNull(storageOptimizeForPerformance);
            this.TestMode = testMode;
        }

        public static Settings Current => DefaultSettings.Value;

        public string DeviceId { get; }

        public string ModuleId { get; }

        public int TwinUpdateSize { get; }

        public TimeSpan TwinUpdateFrequency { get; }

        public TimeSpan TwinUpdateFailureThreshold { get; }

        public TransportType TransportType { get; }

        public Uri AnalyzerUrl { get; }

        public string ServiceClientConnectionString { get; }

        public string StoragePath { get; }

        public bool StorageOptimizeForPerformance { get; }

        public TestMode TestMode { get; }

        public override string ToString()
        {
            Dictionary<string, string> fields = new Dictionary<string, string>();
            fields.Add(nameof(this.DeviceId), this.DeviceId);
            fields.Add(nameof(this.ModuleId), this.ModuleId);
            fields.Add(nameof(this.TwinUpdateSize), this.TwinUpdateSize.ToString());
            fields.Add(nameof(this.TwinUpdateFrequency), this.TwinUpdateFrequency.ToString());
            fields.Add(nameof(this.TwinUpdateFailureThreshold), this.TwinUpdateFailureThreshold.ToString());
            fields.Add(nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType));
            fields.Add(nameof(this.AnalyzerUrl), this.AnalyzerUrl.ToString());
            fields.Add(nameof(this.StoragePath), this.StoragePath);
            fields.Add(nameof(this.StorageOptimizeForPerformance), this.StorageOptimizeForPerformance.ToString());
            fields.Add(nameof(this.TestMode), this.TestMode.ToString());
            return JsonConvert.SerializeObject(fields, Formatting.Indented);
        }
    }
}
