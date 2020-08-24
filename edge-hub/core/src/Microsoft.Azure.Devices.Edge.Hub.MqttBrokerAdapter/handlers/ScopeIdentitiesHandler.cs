// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
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

        public ScopeIdentitiesHandler(IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = connector;
        }

        async Task PublishAddOrUpdateIdentitiesAndAuthChains(IList<IdentityAndAuthChain> identitiesAndAuthChains)
        {
            Events.PublishingAddOrUpdateIdentitiesAndAuthChains(identitiesAndAuthChains);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(identitiesAndAuthChains));
                await this.connector.SendAsync(AddOrUpdateTopic, payload);
            }
            catch (Exception ex)
            {
                Events.ErrorPublishingIdentities(ex);
            }
        }

        async Task PublishRemoveIdentitiesAndAuthChains(IList<string> identities)
        {
            Events.PublishingRemoveIdentities(identities);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(identities));
                await this.connector.SendAsync(RemoveTopic, payload);
            }
            catch (Exception ex)
            {
                Events.ErrorPublishingIdentities(ex);
            }
        }

        async Task GetAndPublishIdentities()
        {
            IList<string> ids = await this.deviceScopeIdentitiesCache.GetAllIds();
            await this.PublishIdentities(ids);
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.DeviceScopeIdentitiesCache;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ScopeIdentitiesHandler>();

            enum EventIds
            {
                PublishingIdentities = IdStart,
                ErrorPublishingIdentities
            }

            public static void PublishingAddOrUpdateIdentitiesAndAuthChains(IList<IdentityAndAuthChain> identitiesAndAuthChains)
            {
                var message = identitiesAndAuthChains.Select(i => $"id: {i.Identity} with auth chain: {i.AuthChain}")
                    .Join(", ");
                Log.LogDebug((int)EventIds.PublishingIdentities, $"Publishing identities: {message} to mqtt broker.");
            }

            public static void ErrorPublishingIdentities(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorPublishingIdentities, ex, $"A problem occurred while publishing identities to mqtt broker.");
            }
        }
    }
}
