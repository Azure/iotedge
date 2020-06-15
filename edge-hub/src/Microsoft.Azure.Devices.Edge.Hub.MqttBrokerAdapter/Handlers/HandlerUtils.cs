// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public static class HandlerUtils
    {
        public static char[] IdentitySegmentSeparator { get; } = new[] { '/' };

        public static Task AddOrRemoveSubscription(this IDeviceListener proxy, bool add, DeviceSubscription subscription)
        {
            if (add)
            {
                return proxy.AddSubscription(subscription);
            }
            else
            {
                return proxy.RemoveSubscription(subscription);
            }
        }

        public static bool IsMatchWithIds(this Match match, Group id1, Group id2)
        {
            if (match.Success)
            {
                var subscriptionId1 = match.Groups["id1"];
                var subscriptionId2 = match.Groups["id2"];

                var id1Match = id1.Success && subscriptionId1.Success && id1.Value == subscriptionId1.Value;
                var id2Match = (id2.Success && subscriptionId2.Success && id2.Value == subscriptionId2.Value) || (!id2.Success && !subscriptionId2.Success);

                return id1Match && id2Match;
            }

            return false;
        }

        public static IIdentity GetIdentityFromMatch(Group id1, Group id2)
        {
            if (id2.Success)
            {
                // FIXME the iothub name should come from somewhere
                return new ModuleIdentity("vikauthtest.azure-devices.net", id1.Value, id2.Value);
            }
            else
            {
                // FIXME the iothub name should come from somewhere
                return new DeviceIdentity("vikauthtest.azure-devices.net", id1.Value);
            }
        }

        public static string GetPropertyBag(IMessage message)
        {
            var properties = new Dictionary<string, string>(message.Properties);

            foreach (KeyValuePair<string, string> systemProperty in message.SystemProperties)
            {
                if (SystemProperties.OutgoingSystemPropertiesMap.TryGetValue(systemProperty.Key, out string onWirePropertyName))
                {
                    properties[onWirePropertyName] = systemProperty.Value;
                }
            }

            return UrlEncodedDictionarySerializer.Serialize(message.Properties.Concat(message.SystemProperties));
        }
    }
}
