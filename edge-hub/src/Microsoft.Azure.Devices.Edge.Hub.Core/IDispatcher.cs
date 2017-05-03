// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDispatcher
    {
        Task Dispatch(IMessage message, ISet<Endpoint> endpoints);

        Task Dispatch(IMessage message, Endpoint endpoint);

        Task<object> CallMethod(string methodName, object parameters, string deviceId);
    }
}
