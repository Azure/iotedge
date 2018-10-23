// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public interface IDeviceConnectivityManager
    {
        event EventHandler DeviceConnected;

        event EventHandler DeviceDisconnected;

        void CallSucceeded();

        void CallTimedOut();
    }
}
