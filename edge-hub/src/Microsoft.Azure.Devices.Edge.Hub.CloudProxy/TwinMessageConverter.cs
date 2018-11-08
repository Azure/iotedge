// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Shared;

    using Newtonsoft.Json;

    public class TwinMessageConverter : IMessageConverter<Twin>
    {
        public Twin FromMessage(IMessage message)
        {
            var twin = new Twin();
            twin.Properties = message.Body.FromBytes<TwinProperties>();
            if (message.SystemProperties.TryGetValue(SystemProperties.Version, out string versionString)
                && long.TryParse(versionString, out long version))
            {
                twin.Version = version;
            }

            return twin;
        }

        public IMessage ToMessage(Twin sourceMessage)
        {
            var json = new StringBuilder();
            using (JsonWriter writer = new JsonTextWriter(new StringWriter(json)))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(TwinNames.Desired);
                writer.WriteRawValue(sourceMessage.Properties.Desired.ToJson());
                writer.WritePropertyName(TwinNames.Reported);
                writer.WriteRawValue(sourceMessage.Properties.Reported.ToJson());
                writer.WriteEndObject();
                writer.Flush();
            }

            byte[] body = Encoding.UTF8.GetBytes(json.ToString());

            var systemProperties = new Dictionary<string, string>
            {
                [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o")
            };

            if (sourceMessage.Version.HasValue)
            {
                systemProperties[SystemProperties.Version] = sourceMessage.Version.ToString();
            }

            return new EdgeMessage(body, new Dictionary<string, string>(), systemProperties);
        }
    }
}
