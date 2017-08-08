// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using Microsoft.Azure.Devices.Client;

    static class Utils
    {
        public static Message GetMessageCopy(byte[] payload, Message message)
        {
            var copy = new Message(payload);

            foreach (var kv in message.Properties)
            {
                copy.Properties.Add(kv.Key, message.Properties[kv.Key]);
            }

            return copy;
        }
    }
}
