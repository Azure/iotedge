// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public interface IAuthenticationChainProvider
    {
        bool TryGetAuthChain(string id, out string authChain);
    }
}
