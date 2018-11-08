// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;

    public interface ILinkHandlerProvider
    {
        ILinkHandler Create(IAmqpLink link, Uri uri);
    }
}
