// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using TestResultCoordinator.Report;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class Settings
    {
        const string DefaultStoragePath = "";
        const ushort DefaultWebHostPort = 5001;

        static readonly Lazy<Settings> DefaultSettings = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json")
                    .AddEnvironmentVariables()
                    .Build();

                return new Settings(
                    configuration.GetValue<string>("trackingId"),
                    configuration.GetValue<string>("eventHubConnectionString"),
                    configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                    configuration.GetValue("webhostPort", DefaultWebHostPort),
                    configuration.GetValue<string>("logAnalyticsWorkspaceId"),
                    configuration.GetValue<string>("logAnalyticsSharedKey"),
                    configuration.GetValue<string>("logAnalyticsLogType"),
                    configuration.GetValue("storagePath", DefaultStoragePath),
                    configuration.GetValue<bool>("optimizeForPerformance", true),
                    configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                    configuration.GetValue("testDuration", TimeSpan.FromHours(1)),
                    configuration.GetValue("verificationDelay", TimeSpan.FromMinutes(15)));
            });

        Settings(
            string trackingId,
            string eventHubConnectionString,
            string deviceId,
            ushort webHostPort,
            string logAnalyticsWorkspaceId,
            string logAnalyticsSharedKey,
            string logAnalyticsLogType,
            string storagePath,
            bool optimizeForPerformance,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            TimeSpan verificationDelay)
        {
            Preconditions.CheckRange(testDuration.Ticks, 1);

            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.EventHubConnectionString = Preconditions.CheckNonWhiteSpace(eventHubConnectionString, nameof(eventHubConnectionString));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.WebHostPort = Preconditions.CheckNotNull(webHostPort, nameof(webHostPort));
            this.LogAnalyticsWorkspaceId = Preconditions.CheckNonWhiteSpace(logAnalyticsWorkspaceId, nameof(logAnalyticsWorkspaceId));
            this.LogAnalyticsSharedKey = Preconditions.CheckNonWhiteSpace(logAnalyticsSharedKey, nameof(logAnalyticsSharedKey));
            this.LogAnalyticsLogType = Preconditions.CheckNonWhiteSpace(logAnalyticsLogType, nameof(logAnalyticsLogType));
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = Preconditions.CheckNotNull(optimizeForPerformance);
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
            this.ResultSources = this.GetResultSources();
            this.DurationBeforeVerification = verificationDelay;
            this.ConsumerGroupName = "$Default";
            this.ReportMetadataList = this.InitializeReportMetadataList();
        }

        public static Settings Current => DefaultSettings.Value;

        public string EventHubConnectionString { get; }

        public string DeviceId { get; }

        public ushort WebHostPort { get; }

        public string TrackingId { get; }

        public string StoragePath { get; }

        public string LogAnalyticsWorkspaceId { get; }

        public string LogAnalyticsSharedKey { get; }

        public string LogAnalyticsLogType { get; }

        public bool OptimizeForPerformance { get; }

        public TimeSpan TestDuration { get; }

        public TimeSpan TestStartDelay { get; }

        public List<string> ResultSources { get; }

        public TimeSpan DurationBeforeVerification { get; }

        public string ConsumerGroupName { get; }

        public List<IReportMetadata> ReportMetadataList { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.TrackingId), this.TrackingId },
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.WebHostPort), this.WebHostPort.ToString() },
                { nameof(this.StoragePath), this.StoragePath },
                { nameof(this.OptimizeForPerformance), this.OptimizeForPerformance.ToString() },
                { nameof(this.TestStartDelay), this.TestDuration.ToString() },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.ResultSources), string.Join("\n", this.ResultSources) },
                { nameof(this.DurationBeforeVerification), this.DurationBeforeVerification.ToString() },
                { nameof(this.ConsumerGroupName), this.ConsumerGroupName },
                { nameof(this.ReportMetadataList), this.ReportMetadataList.ToString() }
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }

        List<IReportMetadata> InitializeReportMetadataList()
        {
            // TODO: Remove this hardcoded list and use twin update instead
            return new List<IReportMetadata>
            {
                new CountingReportMetadata("loadGen1.send", "relayer1.receive", TestOperationResultType.Messages, TestReportType.CountingReport),
                new CountingReportMetadata("relayer1.send", "relayer1.eventHub", TestOperationResultType.Messages, TestReportType.CountingReport),
                new CountingReportMetadata("loadGen2.send", "relayer2.receive", TestOperationResultType.Messages, TestReportType.CountingReport),
                new CountingReportMetadata("relayer2.send", "relayer2.eventHub", TestOperationResultType.Messages, TestReportType.CountingReport)
            };
        }

        List<string> GetResultSources()
        {
            // TODO: Remove this hardcoded list and use environment variables once we've decided on how exactly to set the configuration
            return new List<string> { "loadGen1.send", "relayer1.receive", "relayer1.send", "relayer1.eventHub", "loadGen2.send", "relayer2.receive", "relayer2.send", "relayer2.eventHub", "networkController" };
        }
    }
}
