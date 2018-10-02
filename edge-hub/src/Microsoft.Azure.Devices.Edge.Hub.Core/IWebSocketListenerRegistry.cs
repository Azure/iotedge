// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IWebSocketListenerRegistry
    {
        bool TryRegister(IWebSocketListener webSocketListener);

        bool TryUnregister(string subProtocol, out IWebSocketListener webSocketListener);

        Option<IWebSocketListener> GetListener(IEnumerable<string> subProtocols);
    }
}
