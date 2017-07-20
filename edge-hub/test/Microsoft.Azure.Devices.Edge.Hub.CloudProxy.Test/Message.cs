// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    class Message : IMessage
    {
        public Message(byte[] body)
            : this(body, new Dictionary<string, string>(), new Dictionary<string, string>())
        {
        }

        public Message(byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties)
        {
            this.Body = body;
            this.Properties = properties;
            this.SystemProperties = systemProperties;
        }

        public void Dispose()
        {
        }

        public byte[] Body { get; }

        public IDictionary<string, string> Properties { get; }

        public IDictionary<string, string> SystemProperties { get; }
    }
}
