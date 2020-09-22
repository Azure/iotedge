// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class NotificationHandler<T>
    {
        readonly AsyncLock stateLock = new AsyncLock(1);
        readonly Func<T, Task> notificationStorer;
        readonly Func<T, Task<IEnumerable<BrokerMessage>>> notificationConvertor;
        readonly Func<Task<IEnumerable<BrokerMessage>>> storedNotificationRetriever;

        IMqttBrokerConnector connector;
        bool connected;

        public NotificationHandler(
            Func<T, Task<IEnumerable<BrokerMessage>>> notificationConvertor,
            Func<T, Task> notificationStorer = null,
            Func<Task<IEnumerable<BrokerMessage>>> storedNotificationRetriever = null)
        {
            this.notificationConvertor = Preconditions.CheckNotNull(notificationConvertor);
            this.notificationStorer = notificationStorer;
            this.storedNotificationRetriever = storedNotificationRetriever;
            this.connected = false;
        }

        public void SetConnector(IMqttBrokerConnector connector)
        {
            this.connector = Preconditions.CheckNotNull(connector);
            this.connector.OnConnected += async (sender, args) => await this.OnConnectAsync();
        }

        async Task OnConnectAsync()
        {
            using (await this.stateLock.LockAsync())
            {
                this.connected = true;
                if (this.storedNotificationRetriever != null)
                {
                    var messages = await this.storedNotificationRetriever();
                    await this.SendMessagesAsync(messages);
                }
            }
        }

        public async Task NotifyAsync(T notification)
        {
            using (await this.stateLock.LockAsync())
            {
                if (this.connected)
                {
                    var messages = await this.notificationConvertor(notification);
                    await this.SendMessagesAsync(messages);
                }
                else if (this.notificationStorer == null)
                {
                    Events<T>.NotificationDiscarded();
                }
                else
                {
                    Events<T>.StoringNotification();
                    await this.notificationStorer(notification);
                    Events<T>.NotificationStored();
                }
            }
        }

        async Task SendMessagesAsync(IEnumerable<BrokerMessage> messages)
        {
            if (messages != null)
            {
                var tasks = new List<Task>();
                foreach (var message in messages)
                {
                    tasks.Add(this.SendMessageAsync(message));
                }

                await Task.WhenAll(tasks);
            }
        }

        async Task SendMessageAsync(BrokerMessage message)
        {
            Events<T>.SendingNotification(message);
            try
            {
                var delivered = await this.connector.SendAsync(message.Topic, Encoding.UTF8.GetBytes(message.Payload));
                if (delivered)
                {
                    Events<T>.NotificationSent(message);
                }
                else
                {
                    Events<T>.NotificationNotDelivered(message);
                }
            }
            catch (Exception ex)
            {
                Events<T>.SendingNotificationError(message, ex);
            }
        }
    }

    public class BrokerMessage
    {
        public string Topic { get; }
        public string Payload { get; }

        public BrokerMessage(string topic, string payload)
        {
            this.Topic = topic;
            this.Payload = payload;
        }
    }

    static class Events<T>
    {
        const int IdStart = HubCoreEventIds.NotificationToBroker;
        static readonly ILogger Log = Logger.Factory.CreateLogger<NotificationHandler<T>>();

        enum EventIds
        {
            SendingNotification = IdStart,
            NotificationSent,
            ErrorSendingNotification,
            StoringNotification,
            NotificationStored,
            NotificationDiscarded
        }

        internal static void SendingNotification(BrokerMessage message) => Log.LogDebug((int)EventIds.SendingNotification, $"Publishing notification: [topic={message.Topic}, payload={message.Payload}] to mqtt broker.");

        internal static void NotificationSent(BrokerMessage message) => Log.LogDebug((int)EventIds.NotificationSent, $"Published notification: [topic={message.Topic}, payload={message.Payload}] to mqtt broker.");

        internal static void SendingNotificationError(BrokerMessage message, Exception ex) => Log.LogError((int)EventIds.ErrorSendingNotification, ex, $"Publishing notification failed: [topic={message.Topic}, payload={message.Payload}] to mqtt broker.");

        internal static void NotificationNotDelivered(BrokerMessage message) => Log.LogError((int)EventIds.ErrorSendingNotification, $"Publishing notification failed: [topic={message.Topic}, payload={message.Payload}] to mqtt broker is not delivered.");

        internal static void StoringNotification() => Log.LogDebug((int)EventIds.StoringNotification, "Storing notification while not connected to mqtt broker.");

        internal static void NotificationStored() => Log.LogDebug((int)EventIds.NotificationStored, "Stored notification while not connected to mqtt broker.");

        internal static void NotificationDiscarded() => Log.LogDebug((int)EventIds.NotificationDiscarded, "Notification was discarded while not connected to mqtt broker.");
    }
}
