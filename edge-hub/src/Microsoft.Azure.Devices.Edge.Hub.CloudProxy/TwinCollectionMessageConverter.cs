// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Shared;

    public class TwinCollectionMessageConverter : IMessageConverter<TwinCollection>
    {
        public TwinCollection FromMessage(IMessage message)
        {
            return message.Body.FromBytes<TwinCollection>();
        }

        public IMessage ToMessage(TwinCollection sourceMessage)
        {
            byte[] body = Encoding.UTF8.GetBytes(sourceMessage.ToJson());
            return new EdgeMessage.Builder(body)
                .SetSystemProperties(
                    new Dictionary<string, string>
                    {
                        [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o"),
                        [SystemProperties.Version] = sourceMessage.Version.ToString()
                    })
                .Build();
        }
    }
}
