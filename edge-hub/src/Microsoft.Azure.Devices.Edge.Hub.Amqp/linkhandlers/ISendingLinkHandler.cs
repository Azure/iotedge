// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public interface ISendingLinkHandler : ILinkHandler
    {
        Task SendMessage(IMessage message);
    }
}
