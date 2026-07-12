// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;

    public interface IDeviceConnectivityManager
    {
        event EventHandler DeviceConnected;

        event EventHandler DeviceDisconnected;

        // Unlike DeviceConnected, this event also fires when a short outage recovers
        // from the intermediate Trying state without first reaching Disconnected.
        event EventHandler ConnectivityRecovered;

        Task CallSucceeded();

        Task CallTimedOut();
    }
}
