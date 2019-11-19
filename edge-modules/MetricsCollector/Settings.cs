// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Settings
    {
        static readonly Lazy<Settings> DefaultSettings = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                return new Settings(
                    configuration.GetValue<string>("MessageIdentifier"),
                    configuration.GetValue<string>("AzMonWorkspaceId"),
                    configuration.GetValue<string>("AzMonWorkspaceKey"),
                    configuration.GetValue<string>("AzMonLogType", "edgeHubMetrics"));
            });

        Settings(
            string messageIdentifier,
            string azMonWorkspaceId,
            string azMonWorkspaceKey,
            string azMonLogType)
        {
            this.MessageIdentifier = Preconditions.CheckNonWhiteSpace(messageIdentifier, nameof(messageIdentifier));
            this.AzMonWorkspaceId = Preconditions.CheckNonWhiteSpace(azMonWorkspaceId, nameof(azMonWorkspaceId));
            this.AzMonWorkspaceKey = Preconditions.CheckNonWhiteSpace(azMonWorkspaceKey, nameof(azMonWorkspaceKey));
            this.AzMonLogType = Preconditions.CheckNonWhiteSpace(azMonLogType, nameof(azMonLogType));
        }

        public static Settings Current => DefaultSettings.Value;

        public string MessageIdentifier { get; }

        public string AzMonWorkspaceId { get; }

        [JsonIgnore]
        public string AzMonWorkspaceKey { get; }

        public string AzMonLogType { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
