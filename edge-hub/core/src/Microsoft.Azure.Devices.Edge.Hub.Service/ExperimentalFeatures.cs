// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class ExperimentalFeatures
    {
        public ExperimentalFeatures(bool enabled, bool disableCloudSubscriptions, bool disableConnectivityCheck, bool enableMqttBroker)
        {
            this.Enabled = enabled;
            this.DisableCloudSubscriptions = disableCloudSubscriptions;
            this.DisableConnectivityCheck = disableConnectivityCheck;
            this.EnableMqttBroker = enableMqttBroker;
        }

        public static ExperimentalFeatures Create(IConfiguration experimentalFeaturesConfig, ILogger logger)
        {
            bool enabled = experimentalFeaturesConfig.GetValue("enabled", false);
            bool disableCloudSubscriptions = enabled && experimentalFeaturesConfig.GetValue("disableCloudSubscriptions", false);
            bool disableConnectivityCheck = enabled && experimentalFeaturesConfig.GetValue("disableConnectivityCheck", false);
            bool enableMqttBroker = enabled && experimentalFeaturesConfig.GetValue(Constants.ConfigKey.MqttBrokerEnabled, false);
            var experimentalFeatures = new ExperimentalFeatures(enabled, disableCloudSubscriptions, disableConnectivityCheck, enableMqttBroker);
            logger.LogInformation($"Experimental features configuration: {experimentalFeatures.ToJson()}");
            return experimentalFeatures;
        }

        public static bool IsViaBrokerUpstream(ExperimentalFeatures experimentalFeatures, bool nestedEdgeEnabled, bool hasGatewayHostname)
        {
            bool isLegacyUpstream = !experimentalFeatures.Enabled
                || !experimentalFeatures.EnableMqttBroker
                || !nestedEdgeEnabled
                || !hasGatewayHostname;

            return isLegacyUpstream;
        }

        public bool Enabled { get; }

        public bool DisableCloudSubscriptions { get; }

        public bool DisableConnectivityCheck { get; }

        public bool EnableMqttBroker { get; }
    }
}
