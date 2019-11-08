// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
                    configuration.GetValue(DeviceIdPropertyName, string.Empty),
                    excludedModules,
                    configuration.GetValue(WebhostPortPropertyName, DefaultWebhostPort),
                    configuration.GetValue(ToleranceInMillisecondsPropertyName, DefaultToleranceInMilliseconds),
                    configuration.GetValue(StoragePathPropertyName, DefaultStoragePath),
                    configuration.GetValue(OptimizeForPerformancePropertyName, true));
            });

        Settings(string eventHubCs, string consumerGroupId, string deviceId, IList<string> excludedModuleIds, string webhostPort, double tolerance, string storagePath, bool optimizeForPerformance)
        {
            this.EventHubConnectionString = eventHubCs;
            this.ConsumerGroupId = consumerGroupId;
            this.ExcludedModuleIds = excludedModuleIds;
            this.DeviceId = deviceId;
            this.WebhostPort = webhostPort;
            this.ToleranceInMilliseconds = tolerance;
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = optimizeForPerformance;
        }

        public static Settings Current => Setting.Value;

        public string EventHubConnectionString { get; }

        public string ConsumerGroupId { get; }

        public IList<string> ExcludedModuleIds { get; }

        public string DeviceId { get; }

        public string WebhostPort { get; }

        public double ToleranceInMilliseconds { get; }

        public string StoragePath { get; set; }

        public bool OptimizeForPerformance { get; set; }
    }
}
