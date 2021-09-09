// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public interface ISendingLinkHandler : ILinkHandler
    {
        Task SendMessage(IMessage message);
    }
}
