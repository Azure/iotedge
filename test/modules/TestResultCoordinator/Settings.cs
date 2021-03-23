// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TestResultCoordinator.Reports;

    class Settings
    {
        const string DefaultStoragePath = "";
        const ushort DefaultWebHostPort = 5001;
        const ushort DefaultUnmatchedResultsMaxSize = 10;

        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(TestResultCoordinator));
        internal static Settings Current = Create();

        List<ITestReportMetadata> reportMetadatas = null;

        Settings(
            string trackingId,
            bool useTestResultReportingService,
            bool useResultEventReceivingService,
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
            TimeSpan sendReportFrequency,
            bool logUploadEnabled,
            string storageAccountConnectionString,
            string networkControllerRunProfileName,
            ushort unmatchedResultsMaxSize,
            string testInfo,
            TestMode testMode,
            TimeSpan unmatchedResultTolerance,
            TimeSpan eventHubDelayTolerance)
        {
            Preconditions.CheckRange(testDuration.Ticks, 1);

            if (useResultEventReceivingService)
            {
                this.TestResultEventReceivingServiceSettings = Option.Some(new TestResultEventReceivingServiceSettings()
                {
                    EventHubConnectionString = Preconditions.CheckNonWhiteSpace(eventHubConnectionString, nameof(eventHubConnectionString)),
                    ConsumerGroupName = "$Default"
                });
            }

            if (useTestResultReportingService)
            {
                this.TestResultReportingServiceSettings = Option.Some(new TestResultReportingServiceSettings()
                {
                    StorageAccountConnectionString = Preconditions.CheckNonWhiteSpace(storageAccountConnectionString, nameof(storageAccountConnectionString)),
                    LogAnalyticsLogType = Preconditions.CheckNonWhiteSpace(logAnalyticsLogType, nameof(logAnalyticsLogType)),
                    LogAnalyticsSharedKey = Preconditions.CheckNonWhiteSpace(logAnalyticsSharedKey, nameof(logAnalyticsSharedKey)),
                    LogAnalyticsWorkspaceId = Preconditions.CheckNonWhiteSpace(logAnalyticsWorkspaceId, nameof(logAnalyticsWorkspaceId)),
                    LogUploadEnabled = logUploadEnabled
                });
            }

            this.ConnectivitySpecificSettings = Option.None<ConnectivitySpecificSettings>();
            this.LongHaulSpecificSettings = Option.None<LongHaulSpecificSettings>();
            switch (testMode)
            {
                case TestMode.Connectivity:
                    {
                        this.ConnectivitySpecificSettings = Option.Some(new ConnectivitySpecificSettings()
                        {
                            TestDuration = testDuration,
                            TestVerificationDelay = verificationDelay
                        });
                        break;
                    }

                case TestMode.LongHaul:
                    {
                        this.LongHaulSpecificSettings = Option.Some(new LongHaulSpecificSettings()
                        {
                            SendReportFrequency = sendReportFrequency,
                            UnmatchedResultTolerance = unmatchedResultTolerance,
                            EventHubDelayTolerance = eventHubDelayTolerance
                        });
                        break;
                    }
            }

            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.IoTHubConnectionString = Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.WebHostPort = Preconditions.CheckNotNull(webHostPort, nameof(webHostPort));
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = Preconditions.CheckNotNull(optimizeForPerformance);
            this.TestStartDelay = testStartDelay;
            this.NetworkControllerType = this.GetNetworkControllerType(networkControllerRunProfileName);
            this.UnmatchedResultsMaxSize = Preconditions.CheckRange<ushort>(unmatchedResultsMaxSize, 1);

            this.TestInfo = ModuleUtil.ParseKeyValuePairs(testInfo, Logger, true);
            this.TestInfo.Add("DeviceId", this.DeviceId);
            this.TestMode = testMode;
        }

        private NetworkControllerType GetNetworkControllerType(string networkControllerRunProfileName)
        {
            // TODO: remove this; network controller should report this information.
            switch (networkControllerRunProfileName)
            {
                case "SatelliteGood":
                    return NetworkControllerType.Satellite;
                case "Cellular3G":
                    return NetworkControllerType.Cellular;
                default:
                    return (NetworkControllerType)Enum.Parse(typeof(NetworkControllerType), networkControllerRunProfileName);
            }
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
                configuration.GetValue("useTestResultReportingService", true),
                configuration.GetValue("useResultEventReceivingService", true),
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
                configuration.GetValue("sendReportFrequency", TimeSpan.FromHours(24)),
                configuration.GetValue<bool>("logUploadEnabled", true),
                configuration.GetValue<string>("STORAGE_ACCOUNT_CONNECTION_STRING"),
                configuration.GetValue<string>(TestConstants.NetworkController.RunProfilePropertyName),
                configuration.GetValue<ushort>("UNMATCHED_RESULTS_MAX_SIZE", DefaultUnmatchedResultsMaxSize),
                configuration.GetValue<string>("TEST_INFO"),
                configuration.GetValue("testMode", TestMode.Connectivity),
                configuration.GetValue("unmatchedResultTolerance", TimeSpan.FromMinutes(1)),
                configuration.GetValue("eventHubDelayTolerance", TimeSpan.FromHours(1)));
        }

        public string IoTHubConnectionString { get; }

        public string DeviceId { get; }

        public string ModuleId { get; }

        public ushort WebHostPort { get; }

        public string TrackingId { get; }

        public string StoragePath { get; }

        public bool OptimizeForPerformance { get; }

        public TimeSpan TestStartDelay { get; }

        public bool LogUploadEnabled { get; }

        public SortedDictionary<string, string> TestInfo { get; }

        public NetworkControllerType NetworkControllerType { get; }

        public ushort UnmatchedResultsMaxSize { get; }

        public Option<TestResultEventReceivingServiceSettings> TestResultEventReceivingServiceSettings { get; }

        public Option<TestResultReportingServiceSettings> TestResultReportingServiceSettings { get; }

        public Option<ConnectivitySpecificSettings> ConnectivitySpecificSettings { get; }

        public Option<LongHaulSpecificSettings> LongHaulSpecificSettings { get; }

        public TestMode TestMode { get; }

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
                { nameof(this.TestStartDelay), this.TestStartDelay.ToString() },
                { nameof(this.NetworkControllerType), this.NetworkControllerType.ToString() },
                { nameof(this.TestInfo), JsonConvert.SerializeObject(this.TestInfo) },
                { nameof(this.TestMode), this.TestMode.ToString() }
            };

            this.TestResultEventReceivingServiceSettings.ForEach(settings => fields.Add(nameof(settings.ConsumerGroupName), settings.ConsumerGroupName));
            this.LongHaulSpecificSettings.ForEach(settings =>
            {
                fields.Add(nameof(settings.SendReportFrequency), settings.SendReportFrequency.ToString());
                fields.Add(nameof(settings.UnmatchedResultTolerance), settings.UnmatchedResultTolerance.ToString());
                fields.Add(nameof(settings.EventHubDelayTolerance), settings.EventHubDelayTolerance.ToString());
            });
            this.ConnectivitySpecificSettings.ForEach(settings =>
            {
                fields.Add(nameof(settings.TestDuration), settings.TestDuration.ToString());
                fields.Add(nameof(settings.TestVerificationDelay), settings.TestVerificationDelay.ToString());
            });

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

    internal struct TestResultEventReceivingServiceSettings
    {
        public string EventHubConnectionString;
        public string ConsumerGroupName;
    }

    internal struct TestResultReportingServiceSettings
    {
        public string StorageAccountConnectionString;
        public string LogAnalyticsWorkspaceId;
        public string LogAnalyticsSharedKey;
        public string LogAnalyticsLogType;
        public bool LogUploadEnabled;
    }

    internal struct ConnectivitySpecificSettings
    {
        public TimeSpan TestDuration;
        public TimeSpan TestVerificationDelay;
    }

    internal struct LongHaulSpecificSettings
    {
        public TimeSpan SendReportFrequency;
        public TimeSpan UnmatchedResultTolerance;
        public TimeSpan EventHubDelayTolerance;
    }
}
