// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class MetricsConfigFromTwin
    {
        [JsonConstructor]
        public MetricsConfigFromTwin(
            string schemaVersion,
            IDictionary<string, string> endpoints,
            int scrapeFrequencySecs,
            MetricsFormat metricsFormat,
            SyncTarget syncTarget)
        {
            this.SchemaVersion = Preconditions.CheckNonWhiteSpace(schemaVersion, nameof(schemaVersion));
            this.Endpoints = Preconditions.CheckNotNull(endpoints);
            this.ScrapeFrequencySecs = Preconditions.CheckRange(scrapeFrequencySecs, 0);
            this.MetricsFormat = Preconditions.CheckNotNull(metricsFormat);
            this.SyncTarget = Preconditions.CheckNotNull(syncTarget);
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
