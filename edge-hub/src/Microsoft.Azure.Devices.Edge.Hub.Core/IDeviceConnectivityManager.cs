// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;

    public interface IDeviceConnectivityManager
    {
        void CallSucceeded();
        void CallTimedOut();

        event EventHandler DeviceConnected;
        event EventHandler DeviceDisconnected;
    }
}
