// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Threading.Tasks;

    public interface ILinkHandler
    {
        string CorrelationId { get; }

        IAmqpLink Link { get; }

        Uri LinkUri { get; }

        LinkType Type { get; }

        Task CloseAsync(TimeSpan timeout);

        Task OpenAsync(TimeSpan timeout);
    }
}
