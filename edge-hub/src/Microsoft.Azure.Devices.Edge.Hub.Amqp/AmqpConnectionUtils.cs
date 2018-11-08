// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;

    using Microsoft.Azure.Devices.Edge.Util;

    static class AmqpConnectionUtils
    {
        public static string GetCorrelationId(IAmqpLink link)
        {
            Preconditions.CheckNotNull(link, nameof(link));

            if (link.Settings?.Properties == null)
            {
                throw new ArgumentException("AmqpLink.Settings.Properties should not be null");
            }

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
