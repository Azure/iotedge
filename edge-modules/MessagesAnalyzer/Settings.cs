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
        const string WebhostPortPropertyName = "WebhostPort";
        const string ToleranceInMillisecondsPropertyName = "ToleranceInMilliseconds";
        const string DefaultDeviceId = "device1";
        const string DefaultWebhostPort = "5001";
        const double DefaultToleranceInMilliseconds = 1000 * 60;

        static readonly Lazy<Settings> setting = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json")
                    .AddEnvironmentVariables()
                    .Build();

                
                IList<string> excludedModules = configuration.GetSection(ExcludeModulesIdsPropertyName).Get<List<string>>() ?? new List<string>();

                
                return new Settings(configuration.GetValue<string>(EventHubConnectionStringPropertyValue),
                    configuration.GetValue(DeviceIdPropertyName, DefaultDeviceId),
                    excludedModules,
                    configuration.GetValue(WebhostPortPropertyName, DefaultWebhostPort),
                    configuration.GetValue(ToleranceInMillisecondsPropertyName, DefaultToleranceInMilliseconds));

            });

        Settings(string eventHubCs, string deviceId, IList<string> excludedModuleIds, string webhostPort, double tolerance)
        {
            this.EventHubConnectionString = eventHubCs;
            this.ExcludedModuleIds = excludedModuleIds;
            this.DeviceId = deviceId;
            this.WebhostPort = webhostPort;
            this.ToleranceInMilliseconds = tolerance;
        }

        public static Settings Current => setting.Value;

        public string EventHubConnectionString { get; }

        public IList<string> ExcludedModuleIds { get; }

        public string DeviceId { get; }

        public string WebhostPort { get;}

        public double ToleranceInMilliseconds { get; }
    }
}
