// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Extensions.Logging;

    using static System.FormattableString;

    using IProtocolGatewayMessage = Microsoft.Azure.Devices.ProtocolGateway.Messaging.IMessage;

    /// <summary>
    /// Devices and modules connected to the Edge Hub use protocol independent names for
    /// designating various endpoints. A device for example is expected to send telemetry
    /// to the endpoint designated by the name <c>devices/{deviceId}/messages/events/</c>
    /// where <c>{deviceId}</c> is a placeholder for the device identifier. When using an
    /// MQTT protocol head however we transform these names into MQTT topic names. This
    /// class, in conjunction with the <see cref="MessageAddressConversionConfiguration"/>
    /// class, handles this task.
    /// See documentation for <see cref="TryBuildProtocolAddressFromEdgeHubMessage"/> and
    /// <see cref="TryParseProtocolMessagePropsFromAddress"/> for additional detail on how
    /// this transformation works.
    /// </summary>
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

        /// <summary>
        /// This method takes an endpoint reference (<paramref name="endPointUri"/>) and an
        /// Edge Hub <see cref="IMessage"/> object (<paramref name="message"/>) and builds
        /// an MQTT topic name. The <paramref name="endPointUri"/> refers to a template endpoint
        /// name. A cloud-to-device (C2D) endpoint for example might look like this -
        /// <c>devices/{deviceId}/messages/devicebound</c> where <c>{deviceId}</c> is a place
        /// holder for the identifier of the device that is receiving the C2D message.
        /// </summary>
        /// <param name="endPointUri">Endpoint Uri</param>
        /// <param name="message">Message</param>
        /// <param name="messagePropertiesToSend">Message properties to send</param>
        /// <param name="address">Address</param>
        /// <returns>Returns true if succeeds, otherwise false.</returns>
        public bool TryBuildProtocolAddressFromEdgeHubMessage(string endPointUri, IMessage message, IDictionary<string, string> messagePropertiesToSend, out string address)
        {
            address = null;
            if (this.outboundTemplateMap.TryGetValue(endPointUri, out UriPathTemplate template))
            {
                try
                {
                    address = template.Bind(message.SystemProperties);
                    if (!string.IsNullOrWhiteSpace(address) && messagePropertiesToSend != null && messagePropertiesToSend.Count > 0)
                    {
                        address = Invariant($"{address.TrimEnd('/')}/{UrlEncodedDictionarySerializer.Serialize(messagePropertiesToSend)}");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // An InvalidOperationException exception means that one of the required
                    // template fields was not supplied in the "properties" dictionary. We
                    // handle that by simply returning false.
                    this.logger.LogWarning($"Applying properties ${message.SystemProperties.ToLogString()} on endpoint URI {endPointUri} failed with error {ex}.");
                }
            }

            return !string.IsNullOrEmpty(address);
        }

        /// <summary>
        /// This method examines the <see cref="ProtocolGateway.Messaging.IMessage.Address"/> property
        /// of the <paramref name="message"/> argument - which here is an MQTT topic name - and
        /// extracts features from it that it then uses to populate the <see cref="ProtocolGateway.Messaging.IMessage.Properties"/>
        /// property.
        /// Note: In case of multiple matches, this method uses the first topic. So the order in which the topics are defined matters.
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>Returns true if parse successfully, otherwise false.</returns>
        public bool TryParseProtocolMessagePropsFromAddress(IProtocolGatewayMessage message)
        {
            string address = message.Address;
            var uri = new Uri(address, UriKind.Relative);
            IList<IList<KeyValuePair<string, string>>> matches = this.inboundTable
                .Select(template => template.Match(uri))
                .Where(match => match.Count > 0)
                .ToList();

            if (matches.Count == 0)
            {
                this.logger.LogWarning($"Message address {message.Address} does not match any topic.");
            }
            else
            {
                if (matches.Count > 1)
                {
                    this.logger.LogDebug($"Message address {message.Address} matches more than one topic. Picking first matching topic.");
                }

                foreach (KeyValuePair<string, string> match in matches[0])
                {
                    // If the template has a key called "params" then it contains all the properties set by the user on
                    // the sent message in query string format. So get the value and parse it.
                    if (match.Key == "params")
                    {
                        if (!string.IsNullOrWhiteSpace(match.Value))
                        {
                            UrlEncodedDictionarySerializer.Deserialize(match.Value, 0, message.Properties);
                        }
                    }
                    else
                    {
                        message.Properties.Add(match.Key, match.Value);
                    }
                }
            }

            return matches.Count > 0;
        }
    }
}
