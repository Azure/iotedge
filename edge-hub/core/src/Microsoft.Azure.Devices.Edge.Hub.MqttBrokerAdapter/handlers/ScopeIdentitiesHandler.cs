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
        readonly Task<IDeviceScopeIdentitiesCache> deviceScopeIdentitiesCacheGetter;
        readonly AsyncLock cacheUpdate = new AsyncLock();
        IMqttBrokerConnector connector;

        public ScopeIdentitiesHandler(Task<IDeviceScopeIdentitiesCache> deviceScopeIdentitiesCacheGetter)
        {
            this.deviceScopeIdentitiesCacheGetter = deviceScopeIdentitiesCacheGetter;
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = connector;
            this.connector.EnsureConnected.ContinueWith(_ => this.OnConnect());
        }

        async Task OnConnect()
        {
            using (await this.cacheUpdate.LockAsync())
            {
                var deviceScopeIdentitiesCache = await this.deviceScopeIdentitiesCacheGetter;
                deviceScopeIdentitiesCache.ServiceIdentitiesUpdated +=
                    async (sender, serviceIdentities) => await this.ServiceIdentitiesUpdated(serviceIdentities);

                IList<string> scopeIds = await deviceScopeIdentitiesCache.GetAllIds();
                IList<BrokerServiceIdentity> brokerIds = await ConvertIdsToBrokerServiceIdentities(scopeIds, deviceScopeIdentitiesCache);
                await this.PublishBrokerServiceIdentities(brokerIds);
            }
        }

        async Task ServiceIdentitiesUpdated(IList<string> serviceIdentities)
        {
            try
            {
                var deviceScopeIdentitiesCache = await this.deviceScopeIdentitiesCacheGetter;
                using (await this.cacheUpdate.LockAsync())
                {
                    IList<BrokerServiceIdentity> brokerServiceIdentities = await ConvertIdsToBrokerServiceIdentities(serviceIdentities, deviceScopeIdentitiesCache);
                    await this.PublishBrokerServiceIdentities(brokerServiceIdentities);
                }
            }
            catch (Exception ex)
            {
                Events.ErrorPublishingIdentities(ex);
            }
        }

        static async Task<IList<BrokerServiceIdentity>> ConvertIdsToBrokerServiceIdentities(IList<string> ids, IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            IList<BrokerServiceIdentity> brokerServiceIdentities = new List<BrokerServiceIdentity>();
            foreach (string id in ids)
            {
                brokerServiceIdentities.Add(
                    new BrokerServiceIdentity(id, await deviceScopeIdentitiesCache.GetAuthChain(id)));
            }

            return brokerServiceIdentities;
        }

        async Task PublishBrokerServiceIdentities(IList<BrokerServiceIdentity> brokerServiceIdentities)
        {
            Events.PublishingAddOrUpdateBrokerServiceIdentities(brokerServiceIdentities);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(brokerServiceIdentities));
                await this.connector.SendAsync(Topic, payload, retain: true);
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
                Log.LogDebug((int)EventIds.PublishingAddOrUpdateBrokerServiceIdentity, $"Publishing:" +
                    $" {brokerServiceIdentities.Select(b => $"identity: {b.Identity} with AuthChain: {b.AuthChain}").Join(", ")} to mqtt broker on topic: {Topic}");
            }

            internal static void ErrorPublishingIdentities(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorPublishingIdentity, ex, $"A problem occurred while publishing identities to mqtt broker.");
            }
        }
    }
}
