// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Extensions.Logging;

    public class MessageAddressConverter
    {
        readonly IList<UriPathTemplate> topicTemplateTable;
        readonly UriPathTemplate outboundTemplate;
        readonly ILogger logger = EdgeLogging.LoggerFactory.CreateLogger<MessageAddressConverter>();

        public MessageAddressConverter(MessageAddressConversionConfiguration configuration)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            Preconditions.CheckArgument(configuration.InboundTemplates.Count > 0);
            Preconditions.CheckArgument(configuration.OutboundTemplates.Count > 0);

            this.topicTemplateTable = (from template in configuration.InboundTemplates
                select new UriPathTemplate(template)).ToList();

            this.outboundTemplate = configuration.OutboundTemplates.Select(x => new UriPathTemplate(x)).Single();
        }

        public bool TryDeriveAddress(IDictionary<string, string> properties, out string address)
        {
            try
            {
                address = this.outboundTemplate.Bind(properties);
            }
            catch (InvalidOperationException)
            {
                address = null;
                return false;
            }
            return true;
        }

        public bool TryParseAddressIntoMessageProperties(string address, ProtocolGateway.Messaging.IMessage message)
        {
            bool matched = false;
            foreach (UriPathTemplate uriPathTemplate in this.topicTemplateTable)
            {
                IList<KeyValuePair<string, string>> matches = uriPathTemplate.Match(new Uri(address, UriKind.Relative));

                if (matches.Count == 0)
                {
                    continue;
                }

                if (matched)
                {
                    this.logger.LogDebug("Topic {0} name matches more than one route.", address);
                    break;
                }
                matched = true;

                int variableCount = matches.Count;
                for (int i = 0; i < variableCount; i++)
                {
                    // todo: this will unconditionally set property values - is it acceptable to overwrite existing value?
                    message.Properties.Add(matches[i].Key, matches[i].Value);
                }
            }
            return matched;
        }
    }
}
