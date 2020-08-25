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

    public class ScopeIdentitiesHandler : IMessageProducer
    {
        const string AddOrUpdateTopic = "$internal/identities/addOrUpdate";
        const string RemoveTopic = "$internal/identities/remove";
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly AtomicBoolean connected = new AtomicBoolean(false);
        IMqttBrokerConnector connector;
        ConcurrentDictionary<string, BrokerServiceIdentity> brokerServiceIdentities = new ConcurrentDictionary<string, BrokerServiceIdentity>();

        public ScopeIdentitiesHandler(IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
            this.deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, serviceIdentity) => Task.WhenAll(this.ServiceIdentityUpdated(serviceIdentity));
            this.deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, identity) => Task.WhenAll(this.ServiceIdentityRemoved(identity));
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = connector;
            this.connector.OnConnected += (sender, args) => Task.WhenAll(this.OnConnect());
        }

        async Task OnConnect()
        {
            this.connected.Set(true);
            await this.PublishBrokerServiceIdentities(this.brokerServiceIdentities.Values.ToList());
        }

        async Task ServiceIdentityRemoved(string identity)
        {
            if (this.connected.Get())
            {
                await this.PublishRemoveBrokerIdentityService(identity);
            }
            else
            {
                this.brokerServiceIdentities.TryRemove(identity, out _);
            }
        }

        async Task ServiceIdentityUpdated(ServiceIdentity serviceIdentity)
        {
            BrokerServiceIdentity brokerServiceIdentity = new BrokerServiceIdentity(serviceIdentity.Id, await this.deviceScopeIdentitiesCache.GetAuthChain(serviceIdentity.Id));
            if (this.connected.Get())
            {
                await this.PublishBrokerServiceIdentities(new List<BrokerServiceIdentity>() { brokerServiceIdentity });
            }
            else
            {
                this.brokerServiceIdentities[brokerServiceIdentity.Identity] = brokerServiceIdentity;
            }
        }

        async Task PublishBrokerServiceIdentities(IList<BrokerServiceIdentity> brokerServiceIdentities)
        {
            Events.PublishingAddOrUpdateBrokerServiceIdentities(brokerServiceIdentities);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(brokerServiceIdentities));
                await this.connector.SendAsync(AddOrUpdateTopic, payload);
            }
            catch (Exception ex)
            {
                Events.ErrorPublishingIdentity(ex);
            }
        }

        async Task PublishRemoveBrokerIdentityService(string identity)
        {
            Events.PublishingRemoveBrokerServiceIdentity(identity);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(identity));
                await this.connector.SendAsync(RemoveTopic, payload);
            }
            catch (Exception ex)
            {
                Events.ErrorPublishingIdentity(ex);
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

                Log.LogDebug((int)EventIds.PublishingAddOrUpdateBrokerServiceIdentity, $"Publishing:" +
                    $" {brokerServiceIdentities.Select(b => $"identity: {b.Identity} with AuthChain: {b.AuthChain}").Join(", ")} to mqtt broker");
            }

            internal static void PublishingRemoveBrokerServiceIdentity(string identity)
            {
                Log.LogDebug((int)EventIds.PublishingRemoveBrokerServiceIdentity, $"Publishing Remove identity: {identity}");
            }

            internal static void ErrorPublishingIdentity(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorPublishingIdentity, ex, $"A problem occurred while publishing identity to mqtt broker.");
            }
        }
    }
}
