// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class Configuration
    {
        [JsonConstructor]
        public Configuration(
            string schemaVersion,
            IDictionary<string, string> endpoints,
            int scrapeFrequencySecs,
            MetricsFormat metricsFormat,
            SyncTarget syncTarget)
        {
            this.SchemaVersion = schemaVersion;
            this.Endpoints = endpoints;
            this.ScrapeFrequencySecs = scrapeFrequencySecs;
            this.MetricsFormat = metricsFormat;
            this.SyncTarget = syncTarget;
        }

        public string SchemaVersion { get; }

        public IDictionary<string, string> Endpoints { get; }

        public int ScrapeFrequencySecs { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public MetricsFormat MetricsFormat { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SyncTarget SyncTarget { get; }

        public override string ToString() => JsonConvert.SerializeObject(this);
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