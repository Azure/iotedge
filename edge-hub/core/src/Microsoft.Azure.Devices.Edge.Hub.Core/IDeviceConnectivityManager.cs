// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;

    public interface IDeviceConnectivityManager
    {
        event EventHandler DeviceConnected;

        event EventHandler DeviceDisconnected;

        Task CallSucceeded();

        Task CallTimedOut();
    }
}
