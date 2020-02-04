// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class NullDeviceConnectivityManager : IDeviceConnectivityManager
    {
        public NullDeviceConnectivityManager()
        {
            Events.Created();
        }

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

        static class Events
        {
            const int IdStart = HubCoreEventIds.NullDeviceConnectivityManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<NullDeviceConnectivityManager>();

            enum EventIds
            {
                Created = IdStart
            }

            internal static void Created()
            {
                Log.LogInformation((int)EventIds.Created, $"Device connectivity check disabled");
            }
        }
    }
}
