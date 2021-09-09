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

    class Settings
    {
        internal static Settings Current = Create();

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string moduleId = configuration.GetValue<string>("IOTEDGE_MODULEID");

            return new Settings(
                configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                moduleId,
                configuration.GetValue<string>("TargetModuleId", moduleId),
                configuration.GetValue<int>("TwinUpdateSize", 1),
                configuration.GetValue<TimeSpan>("TwinUpdateFrequency", TimeSpan.FromSeconds(10)),
                configuration.GetValue<TimeSpan>("TwinUpdateFailureThreshold", TimeSpan.FromMinutes(2)),
                configuration.GetValue<TimeSpan>("EdgeHubRestartFailureTolerance", TimeSpan.FromMinutes(2)),
                configuration.GetValue<TransportType>("TransportType", TransportType.Amqp_Tcp_Only),
                configuration.GetValue<string>("AnalyzerUrl", "http://analyzer:15000"),
                configuration.GetValue<string>("testResultCoordinatorUrl"),
                configuration.GetValue<string>("ServiceClientConnectionString"),
                configuration.GetValue<string>("StoragePath"),
                configuration.GetValue<bool>("StorageOptimizeForPerformance", true),
                configuration.GetValue<TwinTestMode>("TwinTestMode", TwinTestMode.TwinAllOperations),
                Option.Maybe(configuration.GetValue<string>("trackingId")),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                configuration.GetValue("testDuration", TimeSpan.FromMilliseconds(-1)));
        }

        Settings(
            string deviceId,
            string moduleId,
            string targetModuleId,
            int twinUpdateSize,
            TimeSpan twinUpdateFrequency,
            TimeSpan twinUpdateFailureThreshold,
            TimeSpan edgeHubRestartFailureTolerance,
            TransportType transportType,
            string analyzerUrl,
            string testResultCoordinatorUrl,
            string serviceClientConnectionString,
            string storagePath,
            bool storageOptimizeForPerformance,
            TwinTestMode testMode,
            Option<string> trackingId,
            TimeSpan testStartDelay,
            TimeSpan testDuration)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.TargetModuleId = Preconditions.CheckNonWhiteSpace(targetModuleId, nameof(targetModuleId));
            this.TwinUpdateSize = Preconditions.CheckRange(twinUpdateSize, 1);
            this.TwinUpdateFrequency = Preconditions.CheckNotNull(twinUpdateFrequency);
            this.TwinUpdateFailureThreshold = Preconditions.CheckNotNull(twinUpdateFailureThreshold);
            this.EdgeHubRestartFailureTolerance = Preconditions.CheckNotNull(edgeHubRestartFailureTolerance);
            this.TransportType = Preconditions.CheckNotNull(transportType);
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.StoragePath = Preconditions.CheckNotNull(storagePath);
            this.StorageOptimizeForPerformance = Preconditions.CheckNotNull(storageOptimizeForPerformance);
            this.TwinTestMode = testMode;
            this.TrackingId = trackingId;
            this.TestStartDelay = testStartDelay;
            this.TestDuration = testDuration;

            if (!string.IsNullOrWhiteSpace(testResultCoordinatorUrl))
            {
                this.ReporterUrl = new Uri(testResultCoordinatorUrl);
                trackingId.Expect(() => new ArgumentNullException(nameof(trackingId)));
            }
            else
            {
                this.ReporterUrl = new Uri(Preconditions.CheckNonWhiteSpace(analyzerUrl, nameof(analyzerUrl)));
            }
        }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public string TargetModuleId { get; }

        public int TwinUpdateSize { get; }

        public TimeSpan TwinUpdateFrequency { get; }

        public TimeSpan TwinUpdateFailureThreshold { get; }

        public TimeSpan EdgeHubRestartFailureTolerance { get; }

        public TransportType TransportType { get; }

        public Uri ReporterUrl { get; }

        public string ServiceClientConnectionString { get; }

        public string StoragePath { get; }

        public bool StorageOptimizeForPerformance { get; }

        public TwinTestMode TwinTestMode { get; }

        public Option<string> TrackingId { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan TestDuration { get; }

        public override string ToString()
        {
            Dictionary<string, string> fields = new Dictionary<string, string>();
            fields.Add(nameof(this.DeviceId), this.DeviceId);
            fields.Add(nameof(this.ModuleId), this.ModuleId);
            fields.Add(nameof(this.TargetModuleId), this.TargetModuleId);
            fields.Add(nameof(this.TwinUpdateSize), this.TwinUpdateSize.ToString());
            fields.Add(nameof(this.TwinUpdateFrequency), this.TwinUpdateFrequency.ToString());
            fields.Add(nameof(this.TwinUpdateFailureThreshold), this.TwinUpdateFailureThreshold.ToString());
            fields.Add(nameof(this.EdgeHubRestartFailureTolerance), this.EdgeHubRestartFailureTolerance.ToString());
            fields.Add(nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType));
            fields.Add(nameof(this.ReporterUrl), this.ReporterUrl.ToString());
            fields.Add(nameof(this.StoragePath), this.StoragePath);
            fields.Add(nameof(this.StorageOptimizeForPerformance), this.StorageOptimizeForPerformance.ToString());
            fields.Add(nameof(this.TwinTestMode), this.TwinTestMode.ToString());
            fields.Add(nameof(this.TrackingId), this.TrackingId.ToString());
            fields.Add(nameof(this.TestStartDelay), this.TestStartDelay.ToString());
            fields.Add(nameof(this.TestDuration), this.TestDuration.ToString());
            return JsonConvert.SerializeObject(fields, Formatting.Indented);
        }
    }
}
