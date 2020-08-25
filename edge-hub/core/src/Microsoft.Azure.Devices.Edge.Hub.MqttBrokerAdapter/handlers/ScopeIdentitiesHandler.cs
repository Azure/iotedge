// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
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
        const string RemoveTopic = "$internal/identities/removeTopic";
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly AtomicBoolean connected = new AtomicBoolean(false);
        IMqttBrokerConnector connector;
        Dictionary<string, IdentityAndAuthChain> brokerServiceIdentities = new Dictionary<string, IdentityAndAuthChain>();

        public ScopeIdentitiesHandler(IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
            this.deviceScopeIdentitiesCache.ServiceIdentityUpdated += (sender, serviceIdentity) => Task.WhenAll(this.ServiceIdentityUpdated(serviceIdentity));
            this.deviceScopeIdentitiesCache.ServiceIdentityRemoved += (sender, identity) => Task.WhenAll(this.ServiceIdentityRemoved(identity));
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = connector;
            Task.WhenAll(this.PublishAddOrUpdateBrokerServiceIdentities(this.brokerServiceIdentities));
            this.connected.Set(true);
        }

        async Task ServiceIdentityRemoved(string identity)
        {
            if (this.connected.Get())
            {
                await this.PublishRemoveBrokerIdentityService(identity);
            }
            else
            {
                this.brokerServiceIdentities.Remove(identity);
            }
        }

        async Task ServiceIdentityUpdated(ServiceIdentity serviceIdentity)
        {
            IdentityAndAuthChain brokerServiceIdentity = new IdentityAndAuthChain(serviceIdentity.Id, await this.deviceScopeIdentitiesCache.GetAuthChain(serviceIdentity.Id));
            if (this.connected.Get())
            {
                await this.PublishAddOrUpdateBrokerServiceIdentity(brokerServiceIdentity);
            }
            else
            {
                this.brokerServiceIdentities.Add(brokerServiceIdentity.Identity, brokerServiceIdentity);
            }
        }

        async Task PublishAddOrUpdateBrokerServiceIdentities(Dictionary<string, IdentityAndAuthChain> brokerServiceIdentities)
        {
            foreach (IdentityAndAuthChain brokerServiceIdentity in brokerServiceIdentities.Values)
            {
                await this.PublishAddOrUpdateBrokerServiceIdentity(brokerServiceIdentity);
            }
        }

        async Task PublishAddOrUpdateBrokerServiceIdentity(IdentityAndAuthChain brokerServiceIdentity)
        {
            Events.PublishingAddOrUpdateBrokerServiceIdentity(brokerServiceIdentity);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(brokerServiceIdentity));
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

            internal static void PublishingAddOrUpdateBrokerServiceIdentity(IdentityAndAuthChain brokerServiceIdentity)
            {
                Log.LogDebug((int)EventIds.PublishingAddOrUpdateBrokerServiceIdentity, $"Publishing identity:" +
                    $" {brokerServiceIdentity.Identity} with authChain: {brokerServiceIdentity.AuthChain} to mqtt broker.");
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
