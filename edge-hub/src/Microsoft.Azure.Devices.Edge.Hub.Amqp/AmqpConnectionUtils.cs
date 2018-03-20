// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    static class AmqpConnectionUtils
    {
        public static string GetCorrelationId(IAmqpLink link)
        {
            Preconditions.CheckNotNull(link, nameof(link));
            Preconditions.CheckNotNull(link.Settings?.Properties, "link.Settings.Properties");

            if (link.Settings.Properties.TryGetValue(IotHubAmqpProperty.ChannelCorrelationId, out object cid))
            {
                string cidString = cid.ToString();
                int separatorIndex = cidString.IndexOf(":", StringComparison.OrdinalIgnoreCase);
                if (separatorIndex > 0)
                {
                    string correlationId = cidString.Substring(separatorIndex + 1);
                    return correlationId;
                }
            }
            return string.Empty;
        }
    }
}
