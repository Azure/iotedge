// Copyright (c) Microsoft. All rights reserved.
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
    using Newtonsoft.Json.Linq;

    public class TwinMessageConverter : IMessageConverter<Twin>
	{
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

            if(sourceMessage.Version.HasValue)
            {
                systemProperties[SystemProperties.Version] = sourceMessage.Version.ToString();
            }

            return new CloudEdgeMessage(body, null, systemProperties);
		}

		public Twin FromMessage(IMessage message)
		{
			Twin twin = new Twin();
            twin.Properties = message.Body.FromBytes<TwinProperties>();
            if (message.SystemProperties.TryGetValue(SystemProperties.Version, out string versionString)
                && long.TryParse(versionString, out long version))
            {
                twin.Version = version;
            }
            return twin;
		}
	}
}
