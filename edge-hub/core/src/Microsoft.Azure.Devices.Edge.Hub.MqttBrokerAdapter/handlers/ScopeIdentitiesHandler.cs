// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// ScopeIdentitiesHandler is responsible for syncing authorized identities from edgeHub core to the Mqtt Broker.
    /// </summary>
    public class ScopeIdentitiesHandler : IMessageProducer
    {
        const string Topic = "$internal/identities";
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly AtomicBoolean connected = new AtomicBoolean(false);
        IMqttBrokerConnector connector;
        IList<BrokerServiceIdentity> lastBrokerServiceIdentityUpdate = new List<BrokerServiceIdentity>();

        public ScopeIdentitiesHandler(IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
            this.deviceScopeIdentitiesCache.ServiceIdentitiesUpdated +=
                async (sender, serviceIdentities) => await this.ServiceIdentitiesUpdated(serviceIdentities);
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = connector;
            this.connector.OnConnected += async (sender, args) => await this.OnConnect();
        }

        async Task OnConnect()
        {
            this.connected.Set(true);
            await this.PublishBrokerServiceIdentities(this.lastBrokerServiceIdentityUpdate);
        }

        async Task ServiceIdentitiesUpdated(IList<ServiceIdentity> serviceIdentities)
        {
            IList<BrokerServiceIdentity> brokerServiceIdentities = new List<BrokerServiceIdentity>();
            foreach (ServiceIdentity serviceIdentity in serviceIdentities)
            {
                brokerServiceIdentities.Add(
                    new BrokerServiceIdentity(serviceIdentity.Id, await this.deviceScopeIdentitiesCache.GetAuthChain(serviceIdentity.Id)));
            }

            if (this.connected.Get())
            {
                await this.PublishBrokerServiceIdentities(brokerServiceIdentities);
            }
            else
            {
                this.lastBrokerServiceIdentityUpdate = brokerServiceIdentities;
            }
        }

        async Task PublishBrokerServiceIdentities(IList<BrokerServiceIdentity> brokerServiceIdentities)
        {
            Events.PublishingAddOrUpdateBrokerServiceIdentities(brokerServiceIdentities);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(brokerServiceIdentities));
                await this.connector.SendAsync(Topic, payload);
            }
            catch (Exception ex)
            {
                Events.ErrorPublishingIdentities(ex);
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.DeviceScopeIdentitiesCache;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ScopeIdentitiesHandler>();

            enum EventIds
            {
                PublishingAddOrUpdateBrokerServiceIdentity = IdStart,
                PublishingRemoveBrokerServiceIdentity,
                ErrorPublishingIdentity
            }

            internal static void PublishingAddOrUpdateBrokerServiceIdentities(IList<BrokerServiceIdentity> brokerServiceIdentities)
            {
                if (brokerServiceIdentities.Count > 0)
                {
                    Log.LogDebug((int)EventIds.PublishingAddOrUpdateBrokerServiceIdentity, $"Publishing:" +
                        $" {brokerServiceIdentities.Select(b => $"identity: {b.Identity} with AuthChain: {b.AuthChain}").Join(", ")} to mqtt broker on topic: {Topic}");
                }
            }

            internal static void ErrorPublishingIdentities(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorPublishingIdentity, ex, $"A problem occurred while publishing identities to mqtt broker.");
            }
        }
    }
}
