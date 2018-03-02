// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using LinkHandlerMakerFunc = System.Func<
        IAmqpLink,
        System.Uri,
        System.Collections.Generic.IDictionary<string, string>,
        Core.IMessageConverter<Azure.Amqp.AmqpMessage>,
        Core.IConnectionProvider,
        ILinkHandler>;

    public class LinkHandlerProvider : ILinkHandlerProvider
    {
        static readonly IDictionary<UriPathTemplate, LinkHandlerMakerFunc> DefaultTemplatesList = new Dictionary<UriPathTemplate, LinkHandlerMakerFunc>
        {
            { Templates.CbsReceiveTemplate, CbsLinkHandler.Create },
            { Templates.DeviceEventsTemplate, EventsLinkHandler.Create },
            { Templates.ModuleEventsTemplate, EventsLinkHandler.Create },
            { Templates.DeviceFromDeviceBoundTemplate, DeviceBoundLinkHandler.Create },
            { Templates.ModuleFromDeviceBoundTemplate, DeviceBoundLinkHandler.Create },
        };

        readonly IConnectionProvider connectionProvider;
        readonly IMessageConverter<AmqpMessage> messageConverter;
        readonly IDictionary<UriPathTemplate, LinkHandlerMakerFunc> templatesList;

        public LinkHandlerProvider(IConnectionProvider connectionProvider, IMessageConverter<AmqpMessage> messageConverter)
            : this(connectionProvider, messageConverter, DefaultTemplatesList)
        { }

        public LinkHandlerProvider(IConnectionProvider connectionProvider, IMessageConverter<AmqpMessage> messageConverter, IDictionary<UriPathTemplate, LinkHandlerMakerFunc> templatesList)
        {
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.templatesList = Preconditions.CheckNotNull(templatesList, nameof(templatesList));
        }

        public ILinkHandler Create(IAmqpLink link, Uri uri)
        {
            foreach (UriPathTemplate template in this.templatesList.Keys)
            {
                if (TryMatchTemplate(uri, template, out IList<KeyValuePair<string, string>> boundVariables))
                {
                    ILinkHandler linkHandler = this.templatesList[template].Invoke(link, uri, boundVariables.ToDictionary(), this.messageConverter, this.connectionProvider);
                    return linkHandler;
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
    }
}
