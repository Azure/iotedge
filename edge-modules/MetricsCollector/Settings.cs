// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    public class Settings
    {
        internal static Settings Current = Create();

        Settings(
            string azMonWorkspaceId,
            string azMonWorkspaceKey,
            string azMonLogType,
            string endpoints,
            int scrapeFrequencySecs,
            string testType,
            UploadTarget uploadTarget)
        {
            this.AzMonWorkspaceId = Preconditions.CheckNonWhiteSpace(azMonWorkspaceId, nameof(azMonWorkspaceId));
            this.AzMonWorkspaceKey = Preconditions.CheckNonWhiteSpace(azMonWorkspaceKey, nameof(azMonWorkspaceKey));
            this.AzMonLogType = Preconditions.CheckNonWhiteSpace(azMonLogType, nameof(azMonLogType));

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
                throw new ArgumentException("No endpoints specified to scrape metrics");
            }

            this.TestType = Preconditions.CheckNonWhiteSpace(testType, nameof(testType));
            this.ScrapeFrequencySecs = Preconditions.CheckRange(scrapeFrequencySecs, 1);
            this.UploadTarget = uploadTarget;
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json")
                .AddEnvironmentVariables()
                .Build();

            return new Settings(
                configuration.GetValue<string>("AzMonWorkspaceId"),
                configuration.GetValue<string>("AzMonWorkspaceKey"),
                configuration.GetValue<string>("AzMonLogType", "testMetrics"),
                configuration.GetValue<string>("MetricsEndpointsCSV", "http://edgeHub:9600/metrics,http://edgeAgent:9600/metrics"),
                configuration.GetValue<int>("ScrapeFrequencyInSecs", 300),
                configuration.GetValue<string>("TestType"),
                configuration.GetValue<UploadTarget>("UploadTarget", UploadTarget.AzureLogAnalytics));
        }

        public string AzMonWorkspaceId { get; }

        public string AzMonWorkspaceKey { get; }

        public string AzMonLogType { get; }

        public List<string> Endpoints { get; }

        public int ScrapeFrequencySecs { get; }

        public string TestType { get; }

        public UploadTarget UploadTarget { get; }

        public override string ToString()
        {
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.AzMonWorkspaceId), this.AzMonWorkspaceId },
                { nameof(this.AzMonLogType), this.AzMonLogType },
                { nameof(this.Endpoints), JsonConvert.SerializeObject(this.Endpoints, Formatting.Indented) },
                { nameof(this.ScrapeFrequencySecs), this.ScrapeFrequencySecs.ToString() },
                { nameof(this.UploadTarget), Enum.GetName(typeof(UploadTarget), this.UploadTarget) }
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }

    public enum UploadTarget
    {
        EventHub,
        AzureLogAnalytics
    }
}
