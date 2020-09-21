// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Newtonsoft.Json;

    /// <summary>
    /// ScopeIdentitiesHandler is responsible for syncing authorized identities from edgeHub core to the Mqtt Broker.
    /// </summary>
    public class ScopeIdentitiesHandler : IMessageProducer
    {
        const string Topic = "$internal/identities";
        readonly Task<IDeviceScopeIdentitiesCache> deviceScopeIdentitiesCacheGetter;
        readonly NotificationHandler<IList<string>> notificationHandler;

        public ScopeIdentitiesHandler(Task<IDeviceScopeIdentitiesCache> deviceScopeIdentitiesCacheGetter)
        {
            this.deviceScopeIdentitiesCacheGetter = deviceScopeIdentitiesCacheGetter;
            this.notificationHandler = new NotificationHandler<IList<string>>(this.ConvertNotificationToMessagesAsync, storedNotificationRetriever: this.RetrieveDeviceScopeIdentitiesCacheAsync);
        }

        public void SetConnector(IMqttBrokerConnector connector) => this.notificationHandler.SetConnector(connector);

        async Task<IEnumerable<Message>> RetrieveDeviceScopeIdentitiesCacheAsync()
        {
            var deviceScopeIdentitiesCache = await this.deviceScopeIdentitiesCacheGetter;
            deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += async (sender, serviceIdentities) => await this.notificationHandler.NotifyAsync(serviceIdentities);

            var brokerServiceIdentities = await deviceScopeIdentitiesCache.GetAllIds();
            return await this.ConvertNotificationToMessagesAsync(brokerServiceIdentities);
        }

        async Task<IEnumerable<Message>> ConvertNotificationToMessagesAsync(IList<string> notification)
        {
            IList<BrokerServiceIdentity> brokerServiceIdentities = await this.ConvertIdsToBrokerServiceIdentitiesAsync(notification);
            return brokerServiceIdentities.Count == 0 ? new Message[0] : new[] { new Message(Topic, JsonConvert.SerializeObject(brokerServiceIdentities)) };
        }

        async Task<IList<BrokerServiceIdentity>> ConvertIdsToBrokerServiceIdentitiesAsync(IList<string> ids)
        {
            var deviceScopeIdentitiesCache = await this.deviceScopeIdentitiesCacheGetter;
            IList<BrokerServiceIdentity> brokerServiceIdentities = new List<BrokerServiceIdentity>();
            foreach (string id in ids)
            {
                brokerServiceIdentities.Add(new BrokerServiceIdentity(id, await deviceScopeIdentitiesCache.GetAuthChain(id)));
            }
            return brokerServiceIdentities;
        }
    }
}
