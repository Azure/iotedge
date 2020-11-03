// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Responsible for listening for EdgeHub config updates (twin updates)
    /// and pushing broker config (authorization policy definition and bridge config)
    /// from EdgeHub to the Mqtt Broker.
    /// </summary>
    public class BrokerConfigUpdateHandler : IMessageProducer
    {
        // !Important: please keep in sync with mqtt-edgehub::command::POLICY_UPDATE_TOPIC
        const string PolicyUpdateTopic = "$internal/authorization/policy";

        // !Important: please keep in sync with mqtt-edgehub::command::BRIDGE_UPDATE_TOPIC
        const string BridgeUpdateTopic = "$internal/bridge/settings";

        readonly Task<IConfigSource> configSource;

        IMqttBrokerConnector connector;

        public BrokerConfigUpdateHandler(Task<IConfigSource> configSource)
        {
            this.configSource = configSource;
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = connector;
            this.connector.EnsureConnected.ContinueWith(_ => this.OnConnect());
        }

        async Task OnConnect()
        {
            var configSource = await this.configSource;
            configSource.ConfigUpdated +=
                async (sender, config) => await this.ConfigUpdateHandler(config);

            var config = await configSource.GetConfig();
            await config.ForEachAsync(async config =>
            {
                await this.ConfigUpdateHandler(config);
            });
        }

        async Task ConfigUpdateHandler(EdgeHubConfig config)
        {
            try
            {
                PolicyUpdate policyUpdate = ConfigToPolicyUpdate(config);
                BridgeConfig bridgeUpdate = ConfigToBridgeUpdate(config);

                Events.PublishPolicyUpdate(policyUpdate);

                var policyPayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(policyUpdate));
                await this.connector.SendAsync(PolicyUpdateTopic, policyPayload, retain: true);

                var bridgePayload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bridgeUpdate));
                await this.connector.SendAsync(BridgeUpdateTopic, bridgePayload, retain: true);
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingPolicy(ex);
            }
        }

        static PolicyUpdate ConfigToPolicyUpdate(EdgeHubConfig config)
        {
            Option<AuthorizationConfig> maybePolicy = config.BrokerConfiguration.FlatMap(
                config => config.Authorizations);

            return maybePolicy.Match(
                policy => new PolicyUpdate(JsonConvert.SerializeObject(policy)),
                () => GetEmptyPolicy());
        }

        static BridgeConfig ConfigToBridgeUpdate(EdgeHubConfig config)
        {
            Option<BridgeConfig> maybeBridgeConfig = config.BrokerConfiguration.FlatMap(
                config => config.Bridges);

            return maybeBridgeConfig.Match(
                config => config,
                () => GetEmptyBridgeConfig());
        }

        static PolicyUpdate GetEmptyPolicy()
        {
            return new PolicyUpdate(@"{""statements"": [ ] }");
        }

        static BridgeConfig GetEmptyBridgeConfig()
        {
            return new BridgeConfig();
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.PolicyUpdateHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<BrokerConfigUpdateHandler>();

            enum EventIds
            {
                PublishPolicy = IdStart,
                ErrorUpdatingPolicy
            }

            internal static void PublishPolicyUpdate(PolicyUpdate update)
            {
                Log.LogDebug((int)EventIds.PublishPolicy, $"Publishing ```{update.Definition}``` to mqtt broker on topic: {BridgeUpdateTopic}");
            }

            internal static void ErrorUpdatingPolicy(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorUpdatingPolicy, ex, $"A problem occurred while updating authorization policy to mqtt broker.");
            }
        }

        /// <summary>
        /// PolicyUpdate is a Data Transfer Object used for sending authorization policy
        /// definition from EdgeHub core to Mqtt Broker.
        /// </summary>
        internal class PolicyUpdate
        {
            [JsonConstructor]
            public PolicyUpdate(string policy)
            {
                this.Definition = Preconditions.CheckNonWhiteSpace(policy, nameof(policy));
            }

            /// <summary>
            /// A string that contains new policy definition in json format.
            /// </summary>
            [JsonProperty("definition")]
            public string Definition { get; }
        }
    }
}
