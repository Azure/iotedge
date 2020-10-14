// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;
    using RoutingMessage = Microsoft.Azure.Devices.Routing.Core.Message;

    public class RoutingMessageConverter : IMessageConverter<IRoutingMessage>
    {
        public IMessage ToMessage(IRoutingMessage routingMessage)
        {
            Preconditions.CheckNotNull(routingMessage, nameof(routingMessage));
            Preconditions.CheckNotNull(routingMessage.Body, nameof(routingMessage.Body));

            IDictionary<string, string> properties = new Dictionary<string, string>();
            if (routingMessage.Properties != null)
            {
                foreach (KeyValuePair<string, string> property in routingMessage.Properties)
                {
                    properties.Add(property);
                }
            }

            IDictionary<string, string> systemProperties = new Dictionary<string, string>();
            if (routingMessage.SystemProperties != null)
            {
                foreach (KeyValuePair<string, string> systemProperty in routingMessage.SystemProperties)
                {
                    systemProperties.Add(systemProperty);
                }
            }

            var edgeMessage = new EdgeMessage(routingMessage.Body, properties, systemProperties, routingMessage.ProcessedPriority);
            return edgeMessage;
        }

        public IRoutingMessage FromMessage(IMessage edgeMessage)
        {
            Preconditions.CheckNotNull(edgeMessage, nameof(edgeMessage));
            Preconditions.CheckNotNull(edgeMessage.Body, nameof(edgeMessage.Body));
            Preconditions.CheckNotNull(edgeMessage.Properties, nameof(edgeMessage.Properties));
            Preconditions.CheckNotNull(edgeMessage.SystemProperties, nameof(edgeMessage.SystemProperties));

            IMessageSource messageSource = this.GetMessageSource(edgeMessage.SystemProperties);
            var routingMessage = new RoutingMessage(messageSource, edgeMessage.Body, edgeMessage.Properties, edgeMessage.SystemProperties);
            return routingMessage;
        }

        internal IMessageSource GetMessageSource(IDictionary<string, string> systemProperties)
        {
            if (systemProperties.TryGetValue(SystemProperties.MessageType, out string messageType)
                && messageType.Equals(Constants.TwinChangeNotificationMessageType, StringComparison.OrdinalIgnoreCase))
            {
                return TwinChangeEventMessageSource.Instance;
            }
            else if (systemProperties.TryGetValue(SystemProperties.ConnectionModuleId, out string moduleId))
            {
                return systemProperties.TryGetValue(SystemProperties.OutputName, out string outputName)
                    ? ModuleMessageSource.Create(moduleId, outputName)
                    : ModuleMessageSource.Create(moduleId);
            }
            else
            {
                return TelemetryMessageSource.Instance;
            }
        }
    }
}
