// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Client;

    static class Utils
    {
        public static Message GetMessageCopy(byte[] payload, Message message)
        {
            var copy = new Message(payload);

            foreach (KeyValuePair<string, string> kv in message.Properties)
            {
                copy.Properties.Add(kv.Key, message.Properties[kv.Key]);
            }

            return copy;
        }

        public static TransportType ToTransportType(string transportName, TransportType defaultTransport)
        {
            TransportType transportType;

            if (transportName == null)
            {
                transportType = defaultTransport;
            }
            else
            {
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(TransportType));
                transportType = (TransportType)converter.ConvertFromInvariantString(transportName);
            }

            return transportType;
        }
    }
}
