// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Settings
    {
        const string DefaultConsumerGroupId = "$Default";
        const string DefaultWebhostPort = "5001";
        const double DefaultToleranceInMilliseconds = 1000 * 60;
        const string DefaultStoragePath = "";

        internal static Settings Current = Create();

        Settings(string eventHubConnectionString, string consumerGroupId, string deviceId, IList<string> excludedModuleIds, string webhostPort, double tolerance, bool logAnalyticsEnabled, string logAnalyticsWorkspaceIdName, string logAnalyticsSharedKeyName, string logAnalyticsLogTypeName, string storagePath, bool storageOptimizeForPerformance)
        {
            this.EventHubConnectionString = Preconditions.CheckNonWhiteSpace(eventHubConnectionString, nameof(eventHubConnectionString));
            this.ConsumerGroupId = Preconditions.CheckNonWhiteSpace(consumerGroupId, nameof(consumerGroupId));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ExcludedModuleIds = excludedModuleIds;
            this.WebhostPort = Preconditions.CheckNonWhiteSpace(webhostPort, nameof(webhostPort));
            this.ToleranceInMilliseconds = Preconditions.CheckRange(tolerance, 0);
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = Preconditions.CheckNotNull(storageOptimizeForPerformance);
            this.LogAnalyticsEnabled = logAnalyticsEnabled;
            this.LogAnalyticsWorkspaceId = logAnalyticsWorkspaceIdName;
            this.LogAnalyticsSharedKey = logAnalyticsSharedKeyName;
            this.LogAnalyticsLogType = logAnalyticsLogTypeName;
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json")
                .AddEnvironmentVariables()
                .Build();

            IList<string> excludedModules = configuration.GetSection("ExcludeModules:Ids").Get<List<string>>() ?? new List<string>();

            return new Settings(
                configuration.GetValue<string>("eventHubConnectionString"),
                configuration.GetValue("ConsumerGroupId", DefaultConsumerGroupId),
                configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                excludedModules,
                configuration.GetValue("WebhostPort", DefaultWebhostPort),
                configuration.GetValue("ToleranceInMilliseconds", DefaultToleranceInMilliseconds),
                configuration.GetValue<bool>("LogAnalyticsEnabled"),
                configuration.GetValue<string>("LogAnalyticsWorkspaceId"),
                configuration.GetValue<string>("LogAnalyticsSharedKey"),
                configuration.GetValue<string>("LogAnalyticsLogType"),
                configuration.GetValue<string>("storagePath", DefaultStoragePath),
                configuration.GetValue<bool>("StorageOptimizeForPerformance", true));
        }

        public string EventHubConnectionString { get; }

        public string ConsumerGroupId { get; }

        public IList<string> ExcludedModuleIds { get; }

        public string DeviceId { get; }

        public string WebhostPort { get; }

        public double ToleranceInMilliseconds { get; }

        public string StoragePath { get; }

        public bool OptimizeForPerformance { get; }

        public bool LogAnalyticsEnabled { get; }

        public string LogAnalyticsWorkspaceId { get; }

        public string LogAnalyticsSharedKey { get; }

        public string LogAnalyticsLogType { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.ConsumerGroupId), this.ConsumerGroupId },
                { nameof(this.ExcludedModuleIds), string.Join(",", this.ExcludedModuleIds) },
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.WebhostPort), this.WebhostPort },
                { nameof(this.ToleranceInMilliseconds), this.ToleranceInMilliseconds.ToString() },
                { nameof(this.OptimizeForPerformance), this.OptimizeForPerformance.ToString() },
                { nameof(this.LogAnalyticsEnabled), this.LogAnalyticsEnabled.ToString() },
                { nameof(this.LogAnalyticsWorkspaceId), this.LogAnalyticsWorkspaceId },
                { nameof(this.LogAnalyticsLogType), this.LogAnalyticsLogType },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
