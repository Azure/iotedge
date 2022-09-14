// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Devices.Edge.Util;

    internal class Settings
    {
        public static Settings Current = Create();
        public static Settings Information = GetInformation();

        private Settings(
            string logAnalyticsWorkspaceId,
            string logAnalyticsWorkspaceKey,
            string endpoints,
            int scrapeFrequencySecs,
            UploadTarget uploadTarget,
            bool compressForUpload,
            bool transformForIoTCentral,
            bool experimentalFeatureAddIdentifyingTags,
            string allowedMetrics,
            string blockedMetrics,
            string resourceId,
            string version,
            TimeSpan iotHubConnectFrequency,
            string azureDomain)
        {
            this.UploadTarget = uploadTarget;
            this.ResourceId = Preconditions.CheckNonWhiteSpace(resourceId, nameof(resourceId));

            if (this.UploadTarget == UploadTarget.AzureMonitor)
            {
                this.LogAnalyticsWorkspaceId = Preconditions.CheckNonWhiteSpace(logAnalyticsWorkspaceId, nameof(logAnalyticsWorkspaceId));
                this.LogAnalyticsWorkspaceKey = Preconditions.CheckNonWhiteSpace(logAnalyticsWorkspaceKey, nameof(logAnalyticsWorkspaceKey));
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
                LoggerUtil.Writer.LogError("No scraping endpoints specified, exiting");
                throw new ArgumentException("No endpoints specified for which to scrape metrics");
            }

            this.ScrapeFrequencySecs = Preconditions.CheckRange(scrapeFrequencySecs, 1);
            this.CompressForUpload = compressForUpload;
            this.TransformForIoTCentral = transformForIoTCentral;
            this.ExperimentalFeatureAddIdentifyingTags = experimentalFeatureAddIdentifyingTags;

            // Create list of allowed metrics. If this list is not empty then any metrics not on it should be discarded.
            this.AllowedMetrics = new MetricFilter(allowedMetrics);
            this.BlockedMetrics = new MetricFilter(blockedMetrics);

            this.Version = version;
            this.IotHubConnectFrequency = iotHubConnectFrequency;
            this.AzureDomain = azureDomain;
        }

        private static Settings Create()
        {
            try
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddEnvironmentVariables()
                    .AddJsonFile("config/versionInfo.json")
                    .Build();

                return new Settings(
                    configuration.GetValue<string>("LogAnalyticsWorkspaceId", null),
                    configuration.GetValue<string>("LogAnalyticsSharedKey", null),
                    configuration.GetValue<string>("MetricsEndpointsCSV", "http://edgeHub:9600/metrics,http://edgeAgent:9600/metrics"),
                    configuration.GetValue<int>("ScrapeFrequencyInSecs", 300),
                    configuration.GetValue<UploadTarget>("UploadTarget", UploadTarget.AzureMonitor),
                    configuration.GetValue<bool>("CompressForUpload", true),
                    configuration.GetValue<bool>("TransformForIoTCentral", false),
                    configuration.GetValue<bool>("experimentalFeatures:AddIdentifyingTags", false),
                    configuration.GetValue<string>("AllowedMetrics", ""),
                    configuration.GetValue<string>("BlockedMetrics", ""),
                    configuration.GetValue<string>("ResourceID", ""),
                    configuration.GetValue<string>("version", ""),
                    configuration.GetValue<TimeSpan>("IotHubConnectFrequency", TimeSpan.FromDays(1)),
                    configuration.GetValue<String>("AzureDomain", "azure.com")
                    );
            }
            catch (ArgumentException e)
            {
                LoggerUtil.Writer.LogCritical("Error reading arguments from environment variables. Make sure all required parameter are present");
                LoggerUtil.Writer.LogCritical(e.ToString());
                Environment.Exit(2);
                throw new Exception();  // to make code analyzers happy (this line will never run)
            }
        }

        private static Settings GetInformation()
        {
            Settings settings = Current;

            Regex regex = new Regex("(subscriptions\\/)(.*?)(\\/resourceGroups\\/)(.*?)(\\/providers\\/)");

            return new Settings(
                settings.LogAnalyticsWorkspaceId,
                settings.LogAnalyticsWorkspaceKey,
                string.Join(',', settings.Endpoints),
                settings.ScrapeFrequencySecs,
                settings.UploadTarget,
                settings.CompressForUpload,
                settings.TransformForIoTCentral,
                settings.ExperimentalFeatureAddIdentifyingTags,
                settings.AllowedMetrics.ToString(),
                settings.BlockedMetrics.ToString(),
                regex.Replace(settings.ResourceId, "$1" + "XXX" + "$3" + "XXX" + "$5"),
                settings.Version,
                settings.IotHubConnectFrequency,
                settings.AzureDomain);
        }

        public string LogAnalyticsWorkspaceId { get; }

        public string LogAnalyticsWorkspaceKey { get; }

        public List<string> Endpoints { get; }

        public int ScrapeFrequencySecs { get; }

        public UploadTarget UploadTarget { get; }

        public bool CompressForUpload { get; }

        public bool TransformForIoTCentral { get; }

        public bool ExperimentalFeatureAddIdentifyingTags { get; }

        public MetricFilter AllowedMetrics { get; }

        public MetricFilter BlockedMetrics { get; }

        public string ResourceId { get; }

        public string Version { get; }

        public TimeSpan IotHubConnectFrequency { get; }

        public string AzureDomain { get; }

        // Used to eliminate secrets when logging the settings
        public override string ToString()
        {
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.LogAnalyticsWorkspaceId), this.LogAnalyticsWorkspaceId ?? string.Empty },
                { nameof(this.Endpoints), JsonConvert.SerializeObject(this.Endpoints, Formatting.Indented) },
                { nameof(this.ScrapeFrequencySecs), this.ScrapeFrequencySecs.ToString() },
                { nameof(this.UploadTarget), Enum.GetName(typeof(UploadTarget), this.UploadTarget) },
                { nameof(this.CompressForUpload), this.CompressForUpload.ToString() },
                { nameof(this.TransformForIoTCentral), this.TransformForIoTCentral.ToString() },
                { nameof(this.ExperimentalFeatureAddIdentifyingTags), this.ExperimentalFeatureAddIdentifyingTags.ToString() },
                { nameof(this.AllowedMetrics), string.Join(",", this.AllowedMetrics.ToString()) },
                { nameof(this.BlockedMetrics), string.Join(",", this.BlockedMetrics.ToString()) },
                { nameof(this.ResourceId), this.ResourceId ?? string.Empty },
                { nameof(this.IotHubConnectFrequency), this.IotHubConnectFrequency.ToString() ?? string.Empty },
                { nameof(this.AzureDomain), this.AzureDomain ?? string.Empty },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }

    public enum UploadTarget
    {
        IotMessage,
        AzureMonitor
    }
}
