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
            address = null;

            IList<string> matchedAddresses = this.outboundTable
                .Select(template => template.Bind(properties))
                // An InvalidOperationException exception means that one of the required
                // template fields was not supplied in the "properties" dictionary. We
                // ignore the failing template and move on to the next one.
                .IgnoreExceptions<string, InvalidOperationException>()
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (matchedAddresses.Count > 1)
            {
                this.logger.LogDebug($"Properties [{string.Join(", ", properties.Select(kvp => $"({kvp.Key}, {kvp.Value})"))}] match more than one template.");
            }

            if (matchedAddresses.Count > 0)
            {
                address = matchedAddresses[0];
            }

            return !string.IsNullOrEmpty(address);
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
