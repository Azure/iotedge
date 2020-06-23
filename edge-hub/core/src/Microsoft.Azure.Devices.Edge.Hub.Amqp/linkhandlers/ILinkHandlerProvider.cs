// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;

    public interface ILinkHandlerProvider
    {
        ILinkHandler Create(IAmqpLink link, Uri uri);
    }
}
