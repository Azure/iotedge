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
        readonly IList<UriPathTemplate> inboundTable;
        readonly IList<UriPathTemplate> outboundTable;
        readonly ILogger logger = Logger.Factory.CreateLogger<MessageAddressConverter>();

        public MessageAddressConverter(MessageAddressConversionConfiguration configuration)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            Preconditions.CheckArgument(configuration.InboundTemplates.Count > 0);
            Preconditions.CheckArgument(configuration.OutboundTemplates.Count > 0);

            this.inboundTable = (from template in configuration.InboundTemplates
                select new UriPathTemplate(template)).ToList();

            this.outboundTable = (from template in configuration.OutboundTemplates
                select new UriPathTemplate(template)).ToList();
        }

        public bool TryDeriveAddress(IDictionary<string, string> properties, out string address)
        {
            bool matched = false;
            string addr = address = null;

            foreach (UriPathTemplate uriPathTemplate in this.outboundTable)
            {
                try
                {
                    addr = uriPathTemplate.Bind(properties);
                }
                catch (InvalidOperationException)
                {
                }

                if (string.IsNullOrEmpty(addr))
                {
                    continue;
                }

                if (matched)
                {
                    this.logger.LogDebug("Properties ({properties.Keys[0]}, ...) match more than one template.");
                    break;
                }

                matched = true;
                address = addr;
            }

            return !string.IsNullOrEmpty(address);
        }

        public bool TryParseAddressIntoMessageProperties(string address, ProtocolGateway.Messaging.IMessage message)
        {
            bool matched = false;
            foreach (UriPathTemplate uriPathTemplate in this.inboundTable)
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
