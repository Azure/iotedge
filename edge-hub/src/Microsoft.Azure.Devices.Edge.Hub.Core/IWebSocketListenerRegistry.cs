// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Edge.Util;

    public interface IWebSocketListenerRegistry
    {
        Option<IWebSocketListener> GetListener(IEnumerable<string> subProtocols);

        bool TryRegister(IWebSocketListener webSocketListener);

        bool TryUnregister(string subProtocol, out IWebSocketListener webSocketListener);
    }
}
