// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class AuthorizedScopesHandler : IMessageProducer
    {
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        IMqttBrokerConnector connector;

        public AuthorizedScopesHandler(IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += async (sender, identities) => await this.PublishIdentities(identities);
            connector.OnConnected += async (sender, args) => await this.GetAndPublishIdentities();
            this.connector = connector;
        }

        async Task PublishIdentities(IList<string> identities)
        {
            Events.PublishingIdentities(identities);
            try
            {
                var payload = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(identities));
                await this.connector.SendAsync("$edgehub/authorization", payload);
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthorizedScopesHandler>();

            enum EventIds
            {
                PublishingIdentities = IdStart,
                ErrorPublishingIdentities
            }

            public static void PublishingIdentities(IList<string> identities)
            {
                Log.LogDebug((int)EventIds.PublishingIdentities, $"Publishing identities {identities.Join(", ")} to mqtt broker.");
            }

            public static void ErrorPublishingIdentities(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorPublishingIdentities, ex, $"A problem occurred while publishing identities to mqtt broker.");
            }
        }
    }
}
