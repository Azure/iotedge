// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Settings
    {
        const string ExcludeModulesIdsPropertyName = "ExcludeModules:Ids";
        const string EventHubConnectionStringPropertyValue = "eventHubConnectionString";
        const string DeviceIdPropertyName = "IOTEDGE_DEVICEID";
        const string ConsumerGroupIdPropertyName = "ConsumerGroupId";
        const string WebhostPortPropertyName = "WebhostPort";
        const string ToleranceInMillisecondsPropertyName = "ToleranceInMilliseconds";
        const string StoragePathPropertyName = "StoragePath";
        const string OptimizeForPerformancePropertyName = "StorageOptimizeForPerformance";
        const string DefaultConsumerGroupId = "$Default";
        const string DefaultWebhostPort = "5001";
        const double DefaultToleranceInMilliseconds = 1000 * 60;
        const string DefaultStoragePath = "";
        const string LogAnalyticEnabledName = "LogAnalyticEnabled";
        const string LogAnalyticWorkspaceIdName = "LogAnalyticWorkspaceId";
        const string LogAnalyticSharedKeyName = "LogAnalyticSharedKey";
        const string LogAnalyticLogTypeName = "LogAnalyticLogType";

        static readonly Lazy<Settings> Setting = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json")
                    .AddEnvironmentVariables()
                    .Build();

                IList<string> excludedModules = configuration.GetSection(ExcludeModulesIdsPropertyName).Get<List<string>>() ?? new List<string>();

                return new Settings(
                    configuration.GetValue<string>(EventHubConnectionStringPropertyValue),
                    configuration.GetValue(ConsumerGroupIdPropertyName, DefaultConsumerGroupId),
                    configuration.GetValue<string>(DeviceIdPropertyName),
                    excludedModules,
                    configuration.GetValue(WebhostPortPropertyName, DefaultWebhostPort),
                    configuration.GetValue(ToleranceInMillisecondsPropertyName, DefaultToleranceInMilliseconds),
                    configuration.GetValue<string>(StoragePathPropertyName, DefaultStoragePath),
                    configuration.GetValue<bool>("StorageOptimizeForPerformance", true),
                    configuration.GetValue<bool>(LogAnalyticEnabledName, false),
                    configuration.GetValue<string>(LogAnalyticWorkspaceIdName),
                    configuration.GetValue<string>(LogAnalyticSharedKeyName),
                    configuration.GetValue<string>(LogAnalyticLogTypeName));
            });

        Settings(string eventHubConnectionString, string consumerGroupId, string deviceId, IList<string> excludedModuleIds, string webhostPort, double tolerance, string storagePath, bool storageOptimizeForPerformance, bool logAnalyticEnabled, string logAnalyticsWorkspaceIdName, string logAnalyticsSharedKeyName, string logAnalyticsLogTypeName)
        {
            this.EventHubConnectionString = Preconditions.CheckNonWhiteSpace(eventHubConnectionString, nameof(eventHubConnectionString));
            this.ConsumerGroupId = Preconditions.CheckNonWhiteSpace(consumerGroupId, nameof(consumerGroupId));
            this.ExcludedModuleIds = excludedModuleIds;
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.WebhostPort = Preconditions.CheckNonWhiteSpace(webhostPort, nameof(webhostPort));
            this.ToleranceInMilliseconds = Preconditions.CheckRange(tolerance, 0);
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = Preconditions.CheckNotNull(storageOptimizeForPerformance);
            this.LogAnalyticEnabled = Preconditions.CheckNotNull(logAnalyticEnabled);
            this.LogAnalyticWorkspaceId = logAnalyticsWorkspaceIdName;
            this.LogAnalyticSharedKey = logAnalyticsSharedKeyName;
            this.LogAnalyticLogType = logAnalyticsLogTypeName;
        }

        public static Settings Current => Setting.Value;

        public string EventHubConnectionString { get; }

        public string ConsumerGroupId { get; }

        public IList<string> ExcludedModuleIds { get; }

        public string DeviceId { get; }

        public string WebhostPort { get; }

        public double ToleranceInMilliseconds { get; }

        public string StoragePath { get; }

        public bool OptimizeForPerformance { get; }

        public bool LogAnalyticEnabled { get; }

        public string LogAnalyticWorkspaceId { get; }

        public string LogAnalyticSharedKey { get; }

        public string LogAnalyticLogType { get; }
    }
}
