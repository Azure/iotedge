// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Settings
    {
        internal static Settings Current = Create();

        Settings(
            string logAnalyticsWorkspaceId,
            string logAnalyticsWorkspaceKey,
            string logAnalyticsLogType,
            string endpoints,
            int scrapeFrequencySecs,
            UploadTarget uploadTarget,
            string messageIdentifier,
            TransportType transportType)
        {
            if (uploadTarget == UploadTarget.AzureLogAnalytics)
            {
                this.LogAnalyticsWorkspaceId = Preconditions.CheckNonWhiteSpace(logAnalyticsWorkspaceId, nameof(logAnalyticsWorkspaceId));
                this.LogAnalyticsWorkspaceKey = Preconditions.CheckNonWhiteSpace(logAnalyticsWorkspaceKey, nameof(logAnalyticsWorkspaceKey));
                this.LogAnalyticsLogType = Preconditions.CheckNonWhiteSpace(logAnalyticsLogType, nameof(logAnalyticsLogType));
            }
            else
            {
                this.MessageIdentifier = Preconditions.CheckNonWhiteSpace(messageIdentifier, nameof(messageIdentifier));
            }

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
                throw new ArgumentException("No endpoints specified for which to scrape metrics");
            }

            this.ScrapeFrequencySecs = Preconditions.CheckRange(scrapeFrequencySecs, 1);
            this.UploadTarget = uploadTarget;
            this.TransportType = transportType;
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json")
                .AddEnvironmentVariables()
                .Build();

            return new Settings(
                configuration.GetValue<string>("LogAnalyticsWorkspaceId"),
                configuration.GetValue<string>("LogAnalyticsSharedKey"),
                configuration.GetValue<string>("LogAnalyticsLogType", "IoTEdgeMetrics"),
                configuration.GetValue<string>("MetricsEndpointsCSV", "http://edgeHub:9600/metrics,http://edgeAgent:9600/metrics"),
                configuration.GetValue<int>("ScrapeFrequencyInSecs", 300),
                configuration.GetValue<UploadTarget>("UploadTarget", UploadTarget.AzureLogAnalytics),
                configuration.GetValue<string>("MessageIdentifier", "IoTEdgeMetrics"),
                configuration.GetValue<TransportType>("TransportType", TransportType.Mqtt_Tcp_Only));
        }

        public string LogAnalyticsWorkspaceId { get; }

        public string LogAnalyticsWorkspaceKey { get; }

        public string LogAnalyticsLogType { get; }

        public List<string> Endpoints { get; }

        public int ScrapeFrequencySecs { get; }

        public UploadTarget UploadTarget { get; }

        public string MessageIdentifier { get; }

        public TransportType TransportType { get; }

        public override string ToString()
        {
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.LogAnalyticsWorkspaceId), this.LogAnalyticsWorkspaceId ?? string.Empty },
                { nameof(this.LogAnalyticsLogType), this.LogAnalyticsLogType ?? string.Empty },
                { nameof(this.Endpoints), JsonConvert.SerializeObject(this.Endpoints, Formatting.Indented) },
                { nameof(this.ScrapeFrequencySecs), this.ScrapeFrequencySecs.ToString() },
                { nameof(this.UploadTarget), Enum.GetName(typeof(UploadTarget), this.UploadTarget) }
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }

    public enum UploadTarget
    {
        IoTHub,
        AzureLogAnalytics
    }
}
