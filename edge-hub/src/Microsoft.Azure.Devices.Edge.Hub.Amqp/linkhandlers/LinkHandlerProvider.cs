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

        readonly IMessageConverter<AmqpMessage> messageConverter;
        readonly IDictionary<UriPathTemplate, LinkHandlerMakerFunc> templatesList;

        public LinkHandlerProvider(IMessageConverter<AmqpMessage> messageConverter)
            : this(messageConverter, DefaultTemplatesList)
        { }

        public LinkHandlerProvider(IMessageConverter<AmqpMessage> messageConverter, IDictionary<UriPathTemplate, LinkHandlerMakerFunc> templatesList)
        {
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.templatesList = Preconditions.CheckNotNull(templatesList, nameof(templatesList));
        }

        public ILinkHandler Create(IAmqpLink link, Uri uri)
        {
            Preconditions.CheckNotNull(link, nameof(link));
            Preconditions.CheckNotNull(uri, nameof(uri));

            foreach (UriPathTemplate template in this.templatesList.Keys)
            {
                if (TryMatchTemplate(uri, template, out IList<KeyValuePair<string, string>> boundVariables))
                {
                    ILinkHandler linkHandler = this.templatesList[template].Invoke(link, uri, boundVariables.ToDictionary(), this.messageConverter);
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
