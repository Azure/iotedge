// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
