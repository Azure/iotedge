// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;

    public static class TelemClient
    {
        private static readonly Lazy<TelemetryClient> instance = new Lazy<TelemetryClient>(GetNewClient);

        // random identifier so different telemetry signals from the same client can be correlated
        public readonly static string tempClientId = Guid.NewGuid().ToString();

        private static Dictionary<string, string> TagDictionary => new Dictionary<string, string>{ ["Hub ID"] = Settings.Current.HubResourceID, ["tempClientId"] = tempClientId };

        public static TelemetryClient Instance => instance.Value;

        public static void TrackTaggedException(Exception e) {
            Instance.TrackException(e, TagDictionary);
        }
        public static void TrackTaggedTrace(string message) {
            Instance.TrackTrace(message, TagDictionary);
        }
        public static void TrackTaggedMetric(string name, double value) {
            Instance.TrackMetric(name, value, TagDictionary);
        }
        public static void TrackTaggedEvent(string name) {
            Instance.TrackEvent(name, TagDictionary);
        }

        public static void TrackTaggedEvent(string name, Dictionary<string, string> tags) {
            // merge the passed dictionary with the standard tags dictionary
            var dicts = new List<Dictionary<string, string>> {TagDictionary, tags};
            var merged = dicts.SelectMany(dict => dict).ToDictionary(pair => pair.Key, pair => pair.Value);
            Instance.TrackEvent(name, merged);
        }

        // Private Methods:
        private static TelemetryClient GetNewClient() {
            // set up Application Insights telemetry
            TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();

            configuration.InstrumentationKey = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY")));

            String telemDisabled = Environment.GetEnvironmentVariable("DISABLE_TELEMETRY");
            if (telemDisabled != null) {
                configuration.DisableTelemetry = true;
            }

            return new TelemetryClient(configuration);
        }

        // Extension Methods:
        public static bool TrackTaggedValue(this Microsoft.ApplicationInsights.Metric metric, double value) {
            return metric.TrackValue(value, tempClientId);
        }
    }
}
