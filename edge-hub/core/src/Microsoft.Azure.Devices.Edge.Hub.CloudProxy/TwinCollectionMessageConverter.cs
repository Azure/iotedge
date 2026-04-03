// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Newtonsoft.Json;

    public class TwinCollectionMessageConverter : IMessageConverter<PropertyCollection>
    {
        public IMessage ToMessage(PropertyCollection sourceMessage)
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(sourceMessage));
            return new EdgeMessage.Builder(body)
                .SetSystemProperties(
                    new Dictionary<string, string>
                    {
                        [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o"),
                        [SystemProperties.Version] = sourceMessage.Version.ToString()
                    })
                .Build();
        }

        public PropertyCollection FromMessage(IMessage message)
        {
            string json = Encoding.UTF8.GetString(message.Body);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            var propertyCollection = new PropertyCollection();
            if (dict != null)
            {
                foreach (var kvp in dict)
                {
                    propertyCollection.Add(kvp.Key, kvp.Value);
                }
            }

            return propertyCollection;
        }
    }
}
