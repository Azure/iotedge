// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;

    public class NullDeviceConnectivityManager : IDeviceConnectivityManager
    {
        public event EventHandler DeviceConnected
        {
            add { }
            remove { }
        }

        public event EventHandler DeviceDisconnected
        {
            add { }
            remove { }
        }

        public Task CallSucceeded() => Task.CompletedTask;

        public Task CallTimedOut() => Task.CompletedTask;
    }
}
