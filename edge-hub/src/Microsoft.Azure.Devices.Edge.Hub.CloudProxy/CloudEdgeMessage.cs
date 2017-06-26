// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    class CloudEdgeMessage : IMessage
    {
        public CloudEdgeMessage(byte[] body, IDictionary<string, string> properties, IDictionary<string, string> systemProperties)
        {
            this.Body = Preconditions.CheckNotNull(body);
            this.Properties = properties ?? ImmutableDictionary<string, string>.Empty;
            this.SystemProperties = systemProperties ?? ImmutableDictionary<string, string>.Empty;
        }

        public void Dispose()
        {
        }

        public byte[] Body { get; }

        public IDictionary<string, string> Properties { get; }

        public IDictionary<string, string> SystemProperties { get; }
    }
}