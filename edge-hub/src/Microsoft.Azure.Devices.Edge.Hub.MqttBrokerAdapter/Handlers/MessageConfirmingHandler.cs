// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MessageConfirmingHandler
    {
        readonly IConnectionRegistry connectionRegistry;

        public MessageConfirmingHandler(IConnectionRegistry connectionRegistry) => this.connectionRegistry = connectionRegistry;

        protected async Task ConfirmMessageAsync(IMessage message, IIdentity identity)
        {
            var proxy = default(IDeviceListener);
            try
            {
                proxy = (await this.connectionRegistry.GetDeviceListenerAsync(identity)).Expect(() => new Exception($"No device listener found for {identity.Id}"));
            }
            catch (Exception)
            {
                Events.MissingProxy(identity.Id);
                return;
            }

            var lockToken = "Unknown";
            try
            {
                lockToken = message.SystemProperties[SystemProperties.LockToken];
                await proxy.ProcessMessageFeedbackAsync(lockToken, FeedbackStatus.Complete);
            }
            catch (Exception ex)
            {
                Events.FailedToConfirm(ex, lockToken, identity.Id);
            }
        }

        protected static string GetPropertyBag(IMessage message)
        {
            var properties = new Dictionary<string, string>(message.Properties);

            foreach (KeyValuePair<string, string> systemProperty in message.SystemProperties)
            {
                if (SystemProperties.OutgoingSystemPropertiesMap.TryGetValue(systemProperty.Key, out string onWirePropertyName))
                {
                    properties[onWirePropertyName] = systemProperty.Value;
                }
            }

            return UrlEncodedDictionarySerializer.Serialize(properties);
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.MessageConfirmingHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<MessageConfirmingHandler>();

            enum EventIds
            {
                MissingProxy = IdStart,
                FailedToConfirm
            }

            public static void MissingProxy(string id) => Log.LogError((int)EventIds.MissingProxy, $"Missing device listener for {id}");
            public static void FailedToConfirm(Exception ex, string lockToken, string id) => Log.LogError((int)EventIds.FailedToConfirm, ex, $"Cannot confirm back delivered message to {id} with token {lockToken}");
        }
    }
}
