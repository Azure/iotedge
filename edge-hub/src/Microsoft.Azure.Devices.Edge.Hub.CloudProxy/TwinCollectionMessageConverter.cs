// Copyright (c) Microsoft. All rights reserved.
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

        public TwinCollection FromMessage(IMessage message)
        {
            return message.Body.FromBytes<TwinCollection>();
        }
    }
}
