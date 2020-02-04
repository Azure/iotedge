// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Reports;

    class Settings
    {
        const string DefaultStoragePath = "";
        const ushort DefaultWebHostPort = 5001;

        internal static Settings Current = Create();

        List<ITestReportMetadata> reportMetadatas = null;

        Settings(
            string trackingId,
            string eventHubConnectionString,
            string iotHubConnectionString,
            string deviceId,
            string moduleId,
            ushort webHostPort,
            string logAnalyticsWorkspaceId,
            string logAnalyticsSharedKey,
            string logAnalyticsLogType,
            string storagePath,
            bool optimizeForPerformance,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            TimeSpan verificationDelay,
            string storageAccountConnectionString)
        {
            Preconditions.CheckRange(testDuration.Ticks, 1);

            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.EventHubConnectionString = Preconditions.CheckNonWhiteSpace(eventHubConnectionString, nameof(eventHubConnectionString));
            this.IoTHubConnectionString = Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.WebHostPort = Preconditions.CheckNotNull(webHostPort, nameof(webHostPort));
            this.LogAnalyticsWorkspaceId = Preconditions.CheckNonWhiteSpace(logAnalyticsWorkspaceId, nameof(logAnalyticsWorkspaceId));
            this.LogAnalyticsSharedKey = Preconditions.CheckNonWhiteSpace(logAnalyticsSharedKey, nameof(logAnalyticsSharedKey));
            this.LogAnalyticsLogType = Preconditions.CheckNonWhiteSpace(logAnalyticsLogType, nameof(logAnalyticsLogType));
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = Preconditions.CheckNotNull(optimizeForPerformance);
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
            this.DurationBeforeVerification = verificationDelay;
            this.ConsumerGroupName = "$Default";
            this.StorageAccountConnectionString = Preconditions.CheckNonWhiteSpace(storageAccountConnectionString, nameof(storageAccountConnectionString));
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json")
                .AddEnvironmentVariables()
                .Build();

            return new Settings(
                configuration.GetValue<string>("trackingId"),
                configuration.GetValue<string>("eventHubConnectionString"),
                configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING"),
                configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue("webhostPort", DefaultWebHostPort),
                configuration.GetValue<string>("logAnalyticsWorkspaceId"),
                configuration.GetValue<string>("logAnalyticsSharedKey"),
                configuration.GetValue<string>("logAnalyticsLogType"),
                configuration.GetValue("storagePath", DefaultStoragePath),
                configuration.GetValue<bool>("optimizeForPerformance", true),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                configuration.GetValue("testDuration", TimeSpan.FromHours(1)),
                configuration.GetValue("verificationDelay", TimeSpan.FromMinutes(15)),
                configuration.GetValue<string>("STORAGE_ACCOUNT_CONNECTION_STRING"));
        }

        public string EventHubConnectionString { get; }

        public string IoTHubConnectionString { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public ushort WebHostPort { get; }

        public string TrackingId { get; }

        public string StoragePath { get; }

        public string LogAnalyticsWorkspaceId { get; }

        public string LogAnalyticsSharedKey { get; }

        public string LogAnalyticsLogType { get; }

        public bool OptimizeForPerformance { get; }

        public TimeSpan TestDuration { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan DurationBeforeVerification { get; }

        public string ConsumerGroupName { get; }

        public string StorageAccountConnectionString { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.TrackingId), this.TrackingId },
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.WebHostPort), this.WebHostPort.ToString() },
                { nameof(this.StoragePath), this.StoragePath },
                { nameof(this.OptimizeForPerformance), this.OptimizeForPerformance.ToString() },
                { nameof(this.TestStartDelay), this.TestDuration.ToString() },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.DurationBeforeVerification), this.DurationBeforeVerification.ToString() },
                { nameof(this.ConsumerGroupName), this.ConsumerGroupName },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }

        internal async Task<List<ITestReportMetadata>> GetReportMetadataListAsync(ILogger logger)
        {
            if (this.reportMetadatas == null)
            {
                RegistryManager rm = RegistryManager.CreateFromConnectionString(this.IoTHubConnectionString);
                Twin moduleTwin = await rm.GetTwinAsync(this.DeviceId, this.ModuleId);
                this.reportMetadatas = TestReportUtil.ParseReportMetadataJson(moduleTwin.Properties.Desired["reportMetadataList"].ToString(), logger);
            }

            return this.reportMetadatas;
        }

        internal async Task<HashSet<string>> GetResultSourcesAsync(ILogger logger)
        {
            HashSet<string> sources = (await this.GetReportMetadataListAsync(logger)).SelectMany(r => r.ResultSources).ToHashSet();
            string[] additionalResultSources = new string[] { };

            foreach (string rs in additionalResultSources)
            {
                sources.Add(rs);
            }

            return sources;
        }
    }
}
