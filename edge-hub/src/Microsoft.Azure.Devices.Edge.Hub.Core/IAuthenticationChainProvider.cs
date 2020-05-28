// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IAuthenticationChainProvider
    {
        public Option<string> GetAuthChain(string id);
    }
}
