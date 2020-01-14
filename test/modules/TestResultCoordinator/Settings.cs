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
    using TestResultCoordinator.Reports;

    class Settings
    {
        const string DefaultStoragePath = "";
        const ushort DefaultWebHostPort = 5001;

        internal static Settings Current = Create();

        HashSet<string> resultSources = null;
        List<IReportMetadata> reportMetadatas = null;

        Settings(
            string trackingId,
            string eventHubConnectionString,
            string serviceClientConnectionString,
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
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
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
                configuration.GetValue<string>("ServiceClientConnectionString"),
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
        }

        public string EventHubConnectionString { get; }

        public string ServiceClientConnectionString { get; }

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
                { nameof(this.DurationBeforeVerification), this.DurationBeforeVerification.ToString() },
                { nameof(this.ConsumerGroupName), this.ConsumerGroupName },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }

        internal List<IReportMetadata> GetReportMetadataList()
        {
            if (this.reportMetadatas == null)
            {
                // TODO: initialize list of report metadata by getting from GetTwin method; and update to become Async method.
                return new List<IReportMetadata>
                {
                    new CountingReportMetadata("loadGen1.send", "relayer1.receive", TestOperationResultType.Messages, TestReportType.CountingReport),
                    new CountingReportMetadata("relayer1.send", "relayer1.eventHub", TestOperationResultType.Messages, TestReportType.CountingReport),
                    new CountingReportMetadata("loadGen2.send", "relayer2.receive", TestOperationResultType.Messages, TestReportType.CountingReport),
                    new CountingReportMetadata("relayer2.send", "relayer2.eventHub", TestOperationResultType.Messages, TestReportType.CountingReport),
                // TODO: Enable Direct Method Cloud-to-Module and Cloud-to-EdgeAgent once the verification scheme is finalized.
                // new CountingReportMetadata("directMethodSender1.send", "directMethodReceiver1.receive", TestOperationResultType.DirectMethod, TestReportType.CountingReport),
                // new CountingReportMetadata("directMethodSender2.send", "directMethodReceiver2.receive", TestOperationResultType.DirectMethod, TestReportType.CountingReport),
                // new CountingReportMetadata("directMethodSender3.send", "directMethodSender3.send", TestOperationResultType.DirectMethod, TestReportType.CountingReport),
                    new TwinCountingReportMetadata("twinTester1.desiredUpdated", "twinTester2.desiredReceived", TestReportType.TwinCountingReport, TwinTestPropertyType.Desired),
                    new TwinCountingReportMetadata("twinTester2.reportedReceived", "twinTester2.reportedUpdated", TestReportType.TwinCountingReport, TwinTestPropertyType.Reported),
                    new TwinCountingReportMetadata("twinTester3.desiredUpdated", "twinTester4.desiredReceived", TestReportType.TwinCountingReport, TwinTestPropertyType.Desired),
                    new TwinCountingReportMetadata("twinTester4.reportedReceived", "twinTester4.reportedUpdated", TestReportType.TwinCountingReport, TwinTestPropertyType.Reported),
                    new DeploymentTestReportMetadata("deploymentTester1.send",  "deploymentTester2.receive")
                };
            }

            return this.reportMetadatas;
        }

        internal HashSet<string> GetResultSources()
        {
            if (this.resultSources == null)
            {
                HashSet<string> sources = GetReportMetadataList().SelectMany(r => new string[] { r.ExpectedSource, r.ActualSource }).ToHashSet();
                string[] additionalResultSources = new string[] {
                    "networkController",
                    "directMethodSender1.send",
                    "directMethodReceiver1.receive",
                    "directMethodSender2.send",
                    "directMethodReceiver2.receive",
                    "directMethodSender3.send",
                    "directMethodSender3.send"
                };

                foreach (string rs in additionalResultSources)
                {
                    sources.Add(rs);
                }
                
                this.resultSources = sources;
            }

            return this.resultSources;
        }
    }
}
