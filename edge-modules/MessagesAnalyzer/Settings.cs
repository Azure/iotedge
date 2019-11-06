// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Extensions.Configuration;

    class Settings
    {
        const string ExcludeModulesIdsPropertyName = "ExcludeModules:Ids";
        const string EventHubConnectionStringPropertyValue = "eventHubConnectionString";
        const string DeviceIdPropertyName = "DeviceId";
        const string ConsumerGroupIdPropertyName = "ConsumerGroupId";
        const string WebhostPortPropertyName = "WebhostPort";
        const string ToleranceInMillisecondsPropertyName = "ToleranceInMilliseconds";
        const string DefaultDeviceId = "device1";
        const string DefaultConsumerGroupId = "$Default";
        const string DefaultWebhostPort = "5001";
        const double DefaultToleranceInMilliseconds = 1000 * 60;
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
                    configuration.GetValue(DeviceIdPropertyName, DefaultDeviceId),
                    excludedModules,
                    configuration.GetValue(WebhostPortPropertyName, DefaultWebhostPort),
                    configuration.GetValue(ToleranceInMillisecondsPropertyName, DefaultToleranceInMilliseconds),
                    configuration.GetValue<string>(LogAnalyticEnabledName),
                    configuration.GetValue<string>(LogAnalyticWorkspaceIdName),
                    configuration.GetValue<string>(LogAnalyticSharedKeyName),
                    configuration.GetValue<string>(LogAnalyticLogTypeName));
            });

        Settings(string eventHubCs, string consumerGroupId, string deviceId, IList<string> excludedModuleIds, string webhostPort, double tolerance, string logAnalyticsEnabled, string logAnalyticsWorkspaceIdName, string logAnalyticsSharedKeyName, string logAnalyticsLogTypeName)
        {
            Boolean enaVal;
            this.EventHubConnectionString = eventHubCs;
            this.ConsumerGroupId = consumerGroupId;
            this.ExcludedModuleIds = excludedModuleIds;
            this.DeviceId = deviceId;
            this.WebhostPort = webhostPort;
            this.ToleranceInMilliseconds = tolerance;
            Boolean.TryParse(logAnalyticsEnabled, out enaVal);
            this.LogAnalyticEnabled = enaVal;
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

        public bool LogAnalyticEnabled { get; }

        public string LogAnalyticWorkspaceId { get; }

        public string LogAnalyticSharedKey { get; }

        public string LogAnalyticLogType { get; }
    }
}
