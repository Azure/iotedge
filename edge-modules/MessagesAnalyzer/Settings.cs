// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
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
        const string DefaultConsumerGroupId = "$Default";
        const string DefaultWebhostPort = "5001";
        const double DefaultToleranceInMilliseconds = 1000 * 60;
        const string LogAnalyticsEnabledName = "LogAnalyticsEnabled";
        const string LogAnalyticsWorkspaceIdName = "LogAnalyticsWorkspaceId";
        const string LogAnalyticsSecretName = "LogAnalyticsSecret";
        const string LogAnalyticsLogTypeName = "LogAnalyticsLogType";

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
                    configuration.GetValue<string>(LogAnalyticsEnabledName),
                    configuration.GetValue<string>(LogAnalyticsWorkspaceIdName),
                    configuration.GetValue<string>(LogAnalyticsSecretName),
                    configuration.GetValue<string>(LogAnalyticsLogTypeName));
            });

        Settings(string eventHubConnectionString, string consumerGroupId, string deviceId, IList<string> excludedModuleIds, string webhostPort, double tolerance, string logAnalyticEnabledText, string logAnalyticsWorkspaceIdName, string logAnalyticsSharedKeyName, string logAnalyticsLogTypeName)
        {
            bool logAnalyticEnabled;
            this.EventHubConnectionString = Preconditions.CheckNonWhiteSpace(eventHubConnectionString, nameof(eventHubConnectionString));
            this.ConsumerGroupId = Preconditions.CheckNonWhiteSpace(consumerGroupId, nameof(consumerGroupId));
            this.ExcludedModuleIds = excludedModuleIds;
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.WebhostPort = Preconditions.CheckNonWhiteSpace(webhostPort, nameof(webhostPort));
            this.ToleranceInMilliseconds = Preconditions.CheckRange(tolerance, 0);
            bool.TryParse(logAnalyticEnabledText, out logAnalyticEnabled);
            this.LogAnalyticsEnabled = logAnalyticEnabled;
            this.LogAnalyticsWorkspaceId = logAnalyticsWorkspaceIdName;
            this.LogAnalyticsSecret = logAnalyticsSharedKeyName;
            this.LogAnalyticsLogType = logAnalyticsLogTypeName;
        }

        public static Settings Current => Setting.Value;

        public string EventHubConnectionString { get; }

        public string ConsumerGroupId { get; }

        public IList<string> ExcludedModuleIds { get; }

        public string DeviceId { get; }

        public string WebhostPort { get; }

        public double ToleranceInMilliseconds { get; }

        public bool LogAnalyticsEnabled { get; }

        public string LogAnalyticsWorkspaceId { get; }

        public string LogAnalyticsSecret { get; }

        public string LogAnalyticsLogType { get; }
    }
}
