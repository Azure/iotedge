// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class ExperimentalFeatures
    {
        public ExperimentalFeatures(bool enabled, bool disableCloudSubscriptions, bool disableConnectivityCheck, bool enableNestedEdge, bool enableMqttBroker)
        {
            this.Enabled = enabled;
            this.DisableCloudSubscriptions = disableCloudSubscriptions;
            this.DisableConnectivityCheck = disableConnectivityCheck;
            this.EnableNestedEdge = enableNestedEdge;
            this.EnableMqttBroker = enableMqttBroker;
        }

        public static ExperimentalFeatures Create(IConfiguration experimentalFeaturesConfig, ILogger logger)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("enabled", false);
            bool disableCloudSubscriptions = enabled && experimentalFeaturesConfig.GetValue("disableCloudSubscriptions", false);
            bool disableConnectivityCheck = enabled && experimentalFeaturesConfig.GetValue("disableConnectivityCheck", false);
            bool enableNestedEdge = enabled && experimentalFeaturesConfig.GetValue(Constants.ConfigKey.NestedEdgeEnabled, false);
            bool enableMqttBroker = enabled && experimentalFeaturesConfig.GetValue(Constants.ConfigKey.MqttBrokerEnabled, false);
            var experimentalFeatures = new ExperimentalFeatures(enabled, disableCloudSubscriptions, disableConnectivityCheck, enableNestedEdge, enableMqttBroker);
            logger.LogInformation($"Experimental features configuration: {experimentalFeatures.ToJson()}");
            return experimentalFeatures;
        }

        public static bool IsViaBrokerUpstream(ExperimentalFeatures experimentalFeatures, bool hasGatewayHostname)
        {
            bool isLegacyUpstream = !experimentalFeatures.Enabled
                || !experimentalFeatures.EnableMqttBroker
                || !experimentalFeatures.EnableNestedEdge
                || !hasGatewayHostname;

            return isLegacyUpstream;
        }

        public bool Enabled { get; }

        public bool DisableCloudSubscriptions { get; }

        public bool DisableConnectivityCheck { get; }

        public bool EnableNestedEdge { get; }

        public bool EnableMqttBroker { get; }
    }
}
