// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LinkHandlerProvider : ILinkHandlerProvider
    {
        static readonly IDictionary<(UriPathTemplate Template, bool IsReceiver), LinkType> DefaultTemplatesList = new Dictionary<(UriPathTemplate, bool), LinkType>
        {
            { (Templates.CbsReceiveTemplate, true), LinkType.Cbs },
            { (Templates.CbsReceiveTemplate, false), LinkType.Cbs },
            { (Templates.DeviceEventsTemplate, true), LinkType.Events },
            { (Templates.ModuleEventsTemplate, true), LinkType.Events },
            { (Templates.ModuleEventsTemplate, false), LinkType.ModuleMessages },
            { (Templates.DeviceFromDeviceBoundTemplate, false), LinkType.C2D },
            { (Templates.ModuleFromDeviceBoundTemplate, false), LinkType.C2D },
            { (Templates.Twin.DeviceBoundMethodCallTemplate, true), LinkType.MethodReceiving },
            { (Templates.Twin.ModuleDeviceBoundMethodCallTemplate, true), LinkType.MethodReceiving },
            { (Templates.Twin.DeviceBoundMethodCallTemplate, false), LinkType.MethodSending },
            { (Templates.Twin.ModuleDeviceBoundMethodCallTemplate, false), LinkType.MethodSending },
            { (Templates.Twin.TwinStreamTemplate, true), LinkType.TwinReceiving },
            { (Templates.Twin.ModuleTwinStreamTemplate, true), LinkType.TwinReceiving },
            { (Templates.Twin.TwinStreamTemplate, false), LinkType.TwinSending },
            { (Templates.Twin.ModuleTwinStreamTemplate, false), LinkType.TwinSending },
        };

        readonly IMessageConverter<AmqpMessage> messageConverter;
        readonly IMessageConverter<AmqpMessage> twinMessageConverter;
        readonly IMessageConverter<AmqpMessage> methodMessageConverter;
        readonly IIdentityProvider identityProvider;
        readonly IDictionary<(UriPathTemplate Template, bool IsReceiver), LinkType> templatesList;

        public LinkHandlerProvider(
            IMessageConverter<AmqpMessage> messageConverter,
            IMessageConverter<AmqpMessage> twinMessageConverter,
            IMessageConverter<AmqpMessage> methodMessageConverter,
            IIdentityProvider identityProvider)
            : this(messageConverter, twinMessageConverter, methodMessageConverter, identityProvider, DefaultTemplatesList)
        {
        }

        public LinkHandlerProvider(
            IMessageConverter<AmqpMessage> messageConverter,
            IMessageConverter<AmqpMessage> twinMessageConverter,
            IMessageConverter<AmqpMessage> methodMessageConverter,
            IIdentityProvider identityProvider,
            IDictionary<(UriPathTemplate Template, bool IsReceiver), LinkType> templatesList)
        {
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.twinMessageConverter = Preconditions.CheckNotNull(twinMessageConverter, nameof(twinMessageConverter));
            this.methodMessageConverter = Preconditions.CheckNotNull(methodMessageConverter, nameof(methodMessageConverter));
            this.identityProvider = Preconditions.CheckNotNull(identityProvider, nameof(identityProvider));
            this.templatesList = Preconditions.CheckNotNull(templatesList, nameof(templatesList));
        }

        public ILinkHandler Create(IAmqpLink link, Uri uri)
        {
            Preconditions.CheckNotNull(link, nameof(link));
            Preconditions.CheckNotNull(uri, nameof(uri));

            (LinkType LinkType, IDictionary<string, string> BoundVariables) match = this.GetLinkType(link, uri);
            ILinkHandler linkHandler = this.GetLinkHandler(match.LinkType, link, uri, match.BoundVariables);
            return linkHandler;
        }

        internal ILinkHandler GetLinkHandler(LinkType linkType, IAmqpLink link, Uri uri, IDictionary<string, string> boundVariables)
        {
            if (linkType == LinkType.Cbs)
            {
                return CbsLinkHandler.Create(link, uri);
            }
            else
            {
                IIdentity identity = this.GetIdentity(boundVariables);
                IConnectionHandler connectionHandler = this.GetConnectionHandler(link, identity);
                switch (linkType)
                {
                    case LinkType.C2D:
                        return new DeviceBoundLinkHandler(identity, link as ISendingAmqpLink, uri, boundVariables, connectionHandler, this.messageConverter);

                    case LinkType.Events:
                        return new EventsLinkHandler(identity, link as IReceivingAmqpLink, uri, boundVariables, connectionHandler, this.messageConverter);

                    case LinkType.ModuleMessages:
                        return new ModuleMessageLinkHandler(identity, link as ISendingAmqpLink, uri, boundVariables, connectionHandler, this.messageConverter);

                    case LinkType.MethodSending:
                        return new MethodSendingLinkHandler(identity, link as ISendingAmqpLink, uri, boundVariables, connectionHandler, this.methodMessageConverter);

                    case LinkType.MethodReceiving:
                        return new MethodReceivingLinkHandler(identity, link as IReceivingAmqpLink, uri, boundVariables, connectionHandler, this.methodMessageConverter);

                    case LinkType.TwinReceiving:
                        return new TwinReceivingLinkHandler(identity, link as IReceivingAmqpLink, uri, boundVariables, connectionHandler, this.twinMessageConverter);

                    case LinkType.TwinSending:
                        return new TwinSendingLinkHandler(identity, link as ISendingAmqpLink, uri, boundVariables, connectionHandler, this.twinMessageConverter);

                    default:
                        throw new InvalidOperationException($"Invalid link type {linkType}");
                }
            }
        }

        internal (LinkType LinkType, IDictionary<string, string> BoundVariables) GetLinkType(IAmqpLink link, Uri uri)
        {
            foreach ((UriPathTemplate Template, bool IsReceiver) key in this.templatesList.Keys)
            {
                if (TryMatchTemplate(uri, key.Template, out IList<KeyValuePair<string, string>> boundVariables) && link.IsReceiver == key.IsReceiver)
                {
                    return (this.templatesList[key], boundVariables.ToDictionary());
                }
            }

            throw new InvalidOperationException($"Matching template not found for uri {uri}");
        }

        static bool TryMatchTemplate(Uri uri, UriPathTemplate template, out IList<KeyValuePair<string, string>> boundVariables)
        {
            bool success;
            (success, boundVariables) = template.Match(new Uri(uri.AbsolutePath, UriKind.Relative));
            return success;
        }

        IConnectionHandler GetConnectionHandler(IAmqpLink link, IIdentity identity)
        {
            var amqpClientConnectionsHandler = link.Session.Connection.FindExtension<IClientConnectionsHandler>();
            if (amqpClientConnectionsHandler == null)
            {
                throw new InvalidOperationException("Expected extension IAmqpClientConnectionsHandler not found on connection");
            }

            return amqpClientConnectionsHandler.GetConnectionHandler(identity);
        }

        IIdentity GetIdentity(IDictionary<string, string> boundVariables)
        {
            if (!boundVariables.TryGetValue(Templates.DeviceIdTemplateParameterName, out string deviceId))
            {
                throw new InvalidOperationException("Link should contain a device Id");
            }

            return boundVariables.TryGetValue(Templates.ModuleIdTemplateParameterName, out string moduleId)
                ? this.identityProvider.Create(deviceId, moduleId)
                : this.identityProvider.Create(deviceId);
        }
    }
}
