// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

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
                    configuration.GetValue<string>("AzMonWorkspaceId"),
                    configuration.GetValue<string>("AzMonWorkspaceKey"),
                    configuration.GetValue<string>("AzMonLogType", "edgeHubMetrics"),
                    configuration.GetValue<string>("MetricsEndpoints"),
                    configuration.GetValue<MetricsFormat>("MetricsFormat"),
                    configuration.GetValue<string>("MessageIdentifier"),
                    configuration.GetValue<string>("SchemaVersion"),
                    configuration.GetValue<int>("ScrapeFrequencyInSecs"),
                    configuration.GetValue<SyncTarget>("SyncTarget"));
                });

        Settings(
            string azMonWorkspaceId,
            string azMonWorkspaceKey,
            string azMonLogType,
            string endpoints,
            MetricsFormat metricsFormat,
            string messageIdentifier,
            string schemaVersion,
            int scrapeFrequencySecs,
            SyncTarget syncTarget)
        {
            this.AzMonWorkspaceId = Preconditions.CheckNonWhiteSpace(azMonWorkspaceId, nameof(azMonWorkspaceId));
            this.AzMonWorkspaceKey = Preconditions.CheckNonWhiteSpace(azMonWorkspaceKey, nameof(azMonWorkspaceKey));
            this.AzMonLogType = Preconditions.CheckNonWhiteSpace(azMonLogType, nameof(azMonLogType));

            // mitigate unintended repeated commas
            this.Endpoints = new List<string>();
            foreach (string endpoint in endpoints.Split(","))
            {
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    this.Endpoints.Add(endpoint);
                }
            }

            if (this.Endpoints.Count == 0)
            {
                throw new ArgumentException("No endpoints specified for metrics scrape");
            }

            this.MetricsFormat = Preconditions.CheckNotNull(metricsFormat);
            this.MessageIdentifier = Preconditions.CheckNonWhiteSpace(messageIdentifier, nameof(messageIdentifier));
            this.SchemaVersion = Preconditions.CheckNotNull(schemaVersion);
            this.ScrapeFrequencySecs = Preconditions.CheckRange(scrapeFrequencySecs, 0);
            this.SyncTarget = Preconditions.CheckNotNull(syncTarget);
        }

        public static Settings Current => DefaultSettings.Value;

        public string AzMonWorkspaceId { get; }

        public string AzMonWorkspaceKey { get; }

        public string AzMonLogType { get; }

        public List<string> Endpoints { get; }

        public MetricsFormat MetricsFormat { get; }

        public string MessageIdentifier { get; }

        public string SchemaVersion { get; }

        public int ScrapeFrequencySecs { get; }

        public SyncTarget SyncTarget { get; }

        public override string ToString()
        {
            Dictionary<string, string> fields = new Dictionary<string, string>();
            fields.Add(nameof(this.AzMonWorkspaceId), this.AzMonWorkspaceId);
            fields.Add(nameof(this.AzMonLogType), this.AzMonLogType);
            fields.Add(nameof(this.Endpoints), JsonConvert.SerializeObject(this.Endpoints, Formatting.Indented));
            fields.Add(nameof(this.MetricsFormat), Enum.GetName(typeof(MetricsFormat), this.MetricsFormat));
            fields.Add(nameof(this.MessageIdentifier), this.MessageIdentifier);
            fields.Add(nameof(this.SchemaVersion), this.SchemaVersion);
            fields.Add(nameof(this.ScrapeFrequencySecs), this.ScrapeFrequencySecs.ToString());
            fields.Add(nameof(this.SyncTarget), Enum.GetName(typeof(SyncTarget), this.SyncTarget));

            return JsonConvert.SerializeObject(fields, Formatting.Indented);
        }
    }

    public enum MetricsFormat
    {
        Prometheus,
        Json
    }

    public enum SyncTarget
    {
        IoTHub,
        AzureLogAnalytics
    }
}
