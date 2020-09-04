// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Devices.Edge.Hub.Core;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.handlers
{
    public abstract class AbstractNotificationHandler<T>
    {
        readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        bool _connected;
        IMqttBrokerConnector _connector;

        public void SetConnector(IMqttBrokerConnector connector)
        {
            _connector = Preconditions.CheckNotNull(connector);
            _connector.OnConnected += async (sender, args) => await OnConnectAsync();
        }

        async Task OnConnectAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _connected = true;
                var messages = await ConvertStoredNotificationsToMessagesAsync();
                await SendMessagesAsync(messages);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task NotifyAsync(T notification)
        {
            await _lock.WaitAsync();
            try
            {
                if (!_connected)
                {
                    Events<T>.StoringNotification();
                    await StoreNotificationAsync(notification);
                    Events<T>.NotificationStored();
                }
                else
                {
                    var messages = await ConvertNotificationToMessagesAsync(notification);
                    await SendMessagesAsync(messages);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        async Task SendMessagesAsync(IEnumerable<Message> messages)
        {
            if (messages != null)
            {
                var tasks = new List<Task>();
                foreach (var message in messages)
                {
                    
                    tasks.Add(SendMessageAsync(message));
                }
                await Task.WhenAll(tasks);
            }
        }

        async Task SendMessageAsync(Message message)
        {
            Events<T>.SendingNotification(message);
            try
            {
                var delivered = await _connector.SendAsync(message.Topic, Encoding.UTF8.GetBytes(message.Payload));
                if (delivered)
                {
                    Events<T>.NotificationSent(message);
                }
                else
                {
                    Events<T>.NotificationNotDelivered(message);
                }
            }
            catch(Exception ex)
            {
                Events<T>.SendingNotificationError(message, ex);
            }
        }


        // Store event if not connected 
        public abstract Task StoreNotificationAsync(T notification);
        // what to send
        public abstract Task<IEnumerable<Message>> ConvertStoredNotificationsToMessagesAsync();
        // convert event to message
        public abstract Task<IEnumerable<Message>> ConvertNotificationToMessagesAsync(T notification);
    }

    public class Message
    {
        public string Topic { get; }
        public string Payload { get;  }

        public Message(string topic, string payload)
        {
            Topic = topic;
            Payload = payload;
        }
    }

    static class Events<T>
    {
        const int IdStart = HubCoreEventIds.NotificationToBroker;
        static readonly ILogger Log = Logger.Factory.CreateLogger<AbstractNotificationHandler<T>>();

        enum EventIds
        {
            SendingNotification = IdStart,
            NotificationSent,
            ErrorSendingNotification,
            StoringNotification,
            NotificationStored
        }

        internal static void SendingNotification(Message message) => Log.LogDebug((int)EventIds.SendingNotification, $"Publishing notification: [topic={message.Topic}, payload={message.Payload}] to mqtt broker.");

        internal static void NotificationSent(Message message) => Log.LogDebug((int)EventIds.NotificationSent, $"Published notification: [topic={message.Topic}, payload={message.Payload}] to mqtt broker.");

        internal static void SendingNotificationError(Message message, Exception ex) => Log.LogError((int)EventIds.ErrorSendingNotification, ex, $"Publishing notification failed: [topic={message.Topic}, payload={message.Payload}] to mqtt broker.");

        internal static void NotificationNotDelivered(Message message) => Log.LogError((int)EventIds.ErrorSendingNotification, $"Publishing notification failed: [topic={message.Topic}, payload={message.Payload}] to mqtt broker is not delivered.");

        internal static void StoringNotification() => Log.LogDebug((int)EventIds.StoringNotification, $"Storing notification while not connected to mqtt broker.");

        internal static void NotificationStored() => Log.LogDebug((int)EventIds.NotificationStored, $"Stored notification while not connected to mqtt broker.");
    }
}
