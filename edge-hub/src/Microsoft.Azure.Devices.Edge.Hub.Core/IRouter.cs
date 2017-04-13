// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    public interface IRouter
    {
        Task RouteMessage(IMessage message);
    }
}
