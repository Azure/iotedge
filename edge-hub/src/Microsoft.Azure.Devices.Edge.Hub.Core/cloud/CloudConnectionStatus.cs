// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    public enum CloudConnectionStatus
    {
        ConnectionEstablished,
        DisconnectedTokenExpired,
        Disconnected,
        TokenNearExpiry
    }
}
