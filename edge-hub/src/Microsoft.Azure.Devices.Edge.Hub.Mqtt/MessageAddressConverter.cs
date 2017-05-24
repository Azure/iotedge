// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.ProtocolGateway;

    public class MessageAddressConverter
    {
        readonly IList<UriPathTemplate> inboundTable;
        readonly IDictionary<string, UriPathTemplate> outboundTemplateMap;
        readonly ILogger logger = Logger.Factory.CreateLogger<MessageAddressConverter>();

        public MessageAddressConverter(MessageAddressConversionConfiguration configuration)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            Preconditions.CheckArgument(configuration.InboundTemplates.Count > 0);
            Preconditions.CheckArgument(configuration.OutboundTemplates.Count > 0);

            this.inboundTable = (from template in configuration.InboundTemplates
                select new UriPathTemplate(template)).ToList();

            this.outboundTemplateMap = new Dictionary<string, UriPathTemplate>();
            foreach (KeyValuePair<string, string> kvp in configuration.OutboundTemplates)
            {
                this.outboundTemplateMap.Add(kvp.Key, new UriPathTemplate(kvp.Value));
            }
        }

        public bool TryDeriveAddress(string endPointUri, IDictionary<string, string> properties, out string address)
        {
            UriPathTemplate template;
            if (this.outboundTemplateMap.TryGetValue(endPointUri, out template))
            {
                try
                {
                    address = template.Bind(properties);
                    if (!string.IsNullOrEmpty(address))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {

                }
            }
            address = null;
            return false;
        }

        public bool TryParseAddressIntoMessageProperties(string address, ProtocolGateway.Messaging.IMessage message)
        {
            var uri = new Uri(address, UriKind.Relative);
            IList<IList<KeyValuePair<string, string>>> matches = this.inboundTable
                .Select(template => template.Match(uri))
                .Where(match => match.Count > 0)
                .ToList();

            if (matches.Count > 1)
            {
                this.logger.LogDebug($"Topic name {address} matches more than one route.");
            }

            if (matches.Count > 0)
            {
                foreach (KeyValuePair<string, string> match in matches[0])
                {
                    // todo: this will unconditionally set property values - is it acceptable to overwrite existing value?
                    message.Properties.Add(match.Key, match.Value);
                }
            }

            return matches.Count > 0;
        }
    }
}
