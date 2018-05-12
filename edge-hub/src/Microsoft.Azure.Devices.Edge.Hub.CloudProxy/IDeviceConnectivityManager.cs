// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
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
