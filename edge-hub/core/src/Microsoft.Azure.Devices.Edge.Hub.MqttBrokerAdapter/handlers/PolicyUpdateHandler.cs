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
    /// and pushing new authorization policy definition from EdgeHub config to the Mqtt Broker.
    /// </summary>
    public class PolicyUpdateHandler : IMessageProducer
    {
        // !Important: please keep in sync with mqtt-edgehub::command::POLICY_UPDATE_TOPIC
        const string Topic = "$internal/authorization/policy";

        readonly Task<IConfigSource> configSource;

        IMqttBrokerConnector connector;

        public PolicyUpdateHandler(Task<IConfigSource> configSource)
        {
            this.configSource = configSource;
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = connector;
            this.connector.OnConnected += async (sender, args) => await this.OnConnect();
        }

        async Task OnConnect()
        {
            var configSource = await this.configSource;
            configSource.ConfigUpdates +=
                async (sender, config) => await this.ConfigUpdateHandler(config);

            var config = await configSource.GetConfig();
            await config.ForEachAsync(async config =>
            {
                var update = ConfigToPolicyUpdate(config);
                await this.PublishPolicyUpdate(update);
            });
        }

        async Task ConfigUpdateHandler(EdgeHubConfig config)
        {
            PolicyUpdate update = ConfigToPolicyUpdate(config);
            await this.PublishPolicyUpdate(update);
        }

        static PolicyUpdate ConfigToPolicyUpdate(EdgeHubConfig config)
        {
            // TODO
            throw new NotImplementedException();
        }

        async Task PublishPolicyUpdate(PolicyUpdate update)
        {
            Events.PublishPolicyUpdate(update);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(update));
                await this.connector.SendAsync(Topic, payload);
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingPolicy(ex);
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.PolicyUpdateHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<PolicyUpdateHandler>();

            enum EventIds
            {
                PublishPolicy = IdStart,
                ErrorUpdatingPolicy
            }

            internal static void PublishPolicyUpdate(PolicyUpdate update)
            {
                Log.LogDebug((int)EventIds.PublishPolicy, $"Publishing ```{update.Definition}``` to mqtt broker on topic: {Topic}");
            }

            internal static void ErrorUpdatingPolicy(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorUpdatingPolicy, ex, $"A problem occurred while updating authorization policy to mqtt broker.");
            }
        }
    }
}
