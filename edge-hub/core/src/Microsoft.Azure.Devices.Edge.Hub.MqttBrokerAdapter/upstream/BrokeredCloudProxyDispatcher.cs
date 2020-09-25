// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class BrokeredCloudProxyDispatcher : IMessageConsumer, IMessageProducer
    {
        IEdgeHub edgeHub;
        TaskCompletionSource<IMqttBrokerConnector> connectorGetter = new TaskCompletionSource<IMqttBrokerConnector>();

        public IReadOnlyCollection<string> Subscriptions => new string[] { "$downstream/#" };

        public void BindEdgeHub(IEdgeHub edgeHub)
        {
            this.edgeHub = edgeHub;
        }

        // FIXME: this should be able to switch state:
        public bool IsActive => true;

        public void SetConnector(IMqttBrokerConnector connector) => this.connectorGetter.SetResult(connector);

        public Task<bool> CloseAsync(IIdentity identity)
        {
            return Task.FromResult(true);
        }

        public Task<IMessage> GetTwinAsync(IIdentity identity)
        {
            return Task.FromResult(new EdgeMessage.Builder(new byte[0]).Build() as IMessage);
        }

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            // FIXME: this will recognize the publications to $downstream
            return Task.FromResult(false);
        }

        public Task<bool> OpenAsync(IIdentity identity)
        {
            return Task.FromResult(true);
        }

        public Task RemoveCallMethodAsync(IIdentity identity)
        {
            return Task.CompletedTask;
        }

        public Task RemoveDesiredPropertyUpdatesAsync(IIdentity identity)
        {
            return Task.CompletedTask;
        }

        public Task SendFeedbackMessageAsync(IIdentity identity, string messageId, FeedbackStatus feedbackStatus)
        {
            return Task.CompletedTask;
        }

        public async Task SendMessageAsync(IIdentity identity, IMessage message)
        {
            // FIXME: should encode the properties:
            var connector = await this.GetConnector();
            await connector.SendAsync($"$upstream/{identity.Id}/messages/events", message.Body); // FIXME should encode a confirmation id
        }

        public Task SendMessageBatchAsync(IIdentity identity, IEnumerable<IMessage> inputMessages)
        {
            return Task.CompletedTask;
        }

        public Task SetupCallMethodAsync(IIdentity identity)
        {
            return Task.CompletedTask;
        }

        public Task SetupDesiredPropertyUpdatesAsync(IIdentity identity)
        {
            return Task.CompletedTask;
        }

        public Task StartListening(IIdentity identity)
        {
            return Task.CompletedTask;
        }

        public Task UpdateReportedPropertiesAsync(IIdentity identity, IMessage reportedPropertiesMessage)
        {
            return Task.CompletedTask;
        }

        async Task<IMqttBrokerConnector> GetConnector()
        {
            // The following acquires the connection by two steps:
            // 1) It waits till the the connector is set. That variable is set by the connector itseld
            //    during initialization, but it is possible that a CouldProxy instance already wants to
            //    communicate upstream (e.g. when edgeHub pulls its twin during startup)
            // 2) It takes time to the connector to actually connect, so the second step waits for
            //    that.
            var connector = await this.connectorGetter.Task;
            await connector.EnsureConnected;

            return connector;
        }
    }
}
