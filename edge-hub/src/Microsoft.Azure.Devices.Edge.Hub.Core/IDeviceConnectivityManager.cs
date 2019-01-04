// Copyright (c) Microsoft. All rights reserved.
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
