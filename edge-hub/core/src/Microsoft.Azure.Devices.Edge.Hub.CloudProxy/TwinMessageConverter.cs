// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Newtonsoft.Json;

    public class TwinMessageConverter : IMessageConverter<TwinProperties>
    {
        public IMessage ToMessage(TwinProperties sourceMessage)
        {
            var json = new StringBuilder();
            using (JsonWriter writer = new JsonTextWriter(new StringWriter(json)))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(TwinNames.Desired);
                writer.WriteRawValue(JsonConvert.SerializeObject(sourceMessage.Desired));
                writer.WritePropertyName(TwinNames.Reported);
                writer.WriteRawValue(JsonConvert.SerializeObject(sourceMessage.Reported));
                writer.WriteEndObject();
                writer.Flush();
            }

            byte[] body = Encoding.UTF8.GetBytes(json.ToString());

            var systemProperties = new Dictionary<string, string>
            {
                [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o")
            };

            if (sourceMessage.Desired.Version > 0 || sourceMessage.Reported.Version > 0)
            {
                long version = Math.Max(sourceMessage.Desired.Version, sourceMessage.Reported.Version);
                systemProperties[SystemProperties.Version] = version.ToString();
            }

            return new EdgeMessage(body, new Dictionary<string, string>(), systemProperties);
        }

        public TwinProperties FromMessage(IMessage message)
        {
            // In v2 SDK, TwinProperties cannot be directly deserialized from bytes the same way.
            // Deserialize to a helper structure and reconstruct.
            string json = Encoding.UTF8.GetString(message.Body);
            var twinData = JsonConvert.DeserializeObject<TwinData>(json);

            var twinProperties = new TwinProperties();

            if (twinData?.Desired != null)
            {
                foreach (var kvp in twinData.Desired)
                {
                    twinProperties.Desired.Add(kvp.Key, kvp.Value);
                }
            }

            if (twinData?.Reported != null)
            {
                foreach (var kvp in twinData.Reported)
                {
                    twinProperties.Reported.Add(kvp.Key, kvp.Value);
                }
            }

            return twinProperties;
        }

        class TwinData
        {
            [JsonProperty("desired")]
            public Dictionary<string, object> Desired { get; set; }

            [JsonProperty("reported")]
            public Dictionary<string, object> Reported { get; set; }
        }
    }
}
