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
    public class ScopeIdentitiesHandler : AbstractNotificationHandler<IList<string>>, IMessageProducer
    {
        const string Topic = "$internal/identities";
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        IList<BrokerServiceIdentity> lastBrokerServiceIdentityUpdate = new List<BrokerServiceIdentity>();

        public ScopeIdentitiesHandler(IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache) : base()
        {
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
            this.deviceScopeIdentitiesCache.ServiceIdentitiesUpdated += async (sender, serviceIdentities) => await NotifyAsync(serviceIdentities);
        }

        public override async Task StoreNotificationAsync(IList<string> notification)
        {
            this.lastBrokerServiceIdentityUpdate = await ConvertIdsToBrokerServiceIdentitiesAsync(notification);
        }

        public override Task<IEnumerable<Message>> ConvertStoredNotificationsToMessagesAsync()
        {
            IEnumerable<Message> messages;
            if (this.lastBrokerServiceIdentityUpdate.Count == 0)
            {
                messages = new Message[0];
            }
            else
            {
                messages = new[] { new Message(Topic, JsonConvert.SerializeObject(lastBrokerServiceIdentityUpdate)) };
                this.lastBrokerServiceIdentityUpdate.Clear();
            }

            return Task.FromResult(messages);

        }

        public override async Task<IEnumerable<Message>> ConvertNotificationToMessagesAsync(IList<string> notification)
        {
            IEnumerable<Message> messages;
            if (notification?.Count > 0)
            {
                IList<BrokerServiceIdentity> brokerServiceIdentities = await this.ConvertIdsToBrokerServiceIdentitiesAsync(notification);
                messages = new[] { new Message(Topic, JsonConvert.SerializeObject(brokerServiceIdentities)) };
            }
            else
            {
                messages = new Message[0];
            }

            return messages;
        }

        async Task<IList<BrokerServiceIdentity>> ConvertIdsToBrokerServiceIdentitiesAsync(IList<string> ids)
        {
            IList<BrokerServiceIdentity> brokerServiceIdentities = new List<BrokerServiceIdentity>();
            foreach (string id in ids)
            {
                brokerServiceIdentities.Add(new BrokerServiceIdentity(id, await this.deviceScopeIdentitiesCache.GetAuthChain(id)));
            }

            return brokerServiceIdentities;
        }
    }
}
