// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Stateless;
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using System.Timers;
    using static System.FormattableString;

    /// <summary>
    /// This class checks for the connectivity of the Edge device and raises
    /// events if the device is disconnected, or gets re-connected. 
    /// It does this by relying on success / failure of the various IotHub operations 
    /// performed on behalf of the connected clients and even edge hub.
    /// If there is no activity, then it makes a dummy IoThub call periodically to check if the 
    /// device is connected.
    /// 
    /// This class maintains a state machine with the following logic - 
    /// - If an Iothub call succeeds, the device is in a connected state
    /// - If a call fails, it goes to the Trying state, and tries to make another Iothub call. 
    /// - If this call also fails, it goes into the disconnected state (else goes back to connected state)
    /// - In disconnected state, it tries to connect to IotHub periodically. If a call succeeds, then 
    /// then it goes back to connected state
    /// </summary>
    public class DeviceConnectivityManager : IDeviceConnectivityManager
    {
        enum State
        {
            Connected,
            Trying,
            Disconnected,
        }

        enum Trigger
        {
            CallSucceeded,
            CallTimedOut
        }

        State state;
        ConnectivityChecker connectivityChecker;
        readonly StateMachine<State, Trigger> machine;
        readonly Timer connectedTimer;
        readonly Timer disconnectedTimer;

        public event EventHandler DeviceConnected;
        public event EventHandler DeviceDisconnected;

        public DeviceConnectivityManager(TimeSpan minConnectivityCheckFrequency, TimeSpan disconnectedCheckFrequency)
        {
            this.connectedTimer = new Timer(minConnectivityCheckFrequency.TotalMilliseconds);
            this.disconnectedTimer = new Timer(disconnectedCheckFrequency.TotalMilliseconds);
            this.machine = new StateMachine<State, Trigger>(() => this.state, s => this.state = s);

            this.connectedTimer.Elapsed += (_, __) => this.CheckConnectivity();
            this.disconnectedTimer.Elapsed += (_, __) => this.CheckConnectivity();

            this.machine.Configure(State.Connected)
                .Permit(Trigger.CallTimedOut, State.Trying)
                .InternalTransition(Trigger.CallSucceeded, () => this.ResetConnectedTimer())
                .OnEntry(this.OnConnected)
                .OnExit(this.OnConnectedExit);

            this.machine.Configure(State.Trying)
                .Permit(Trigger.CallTimedOut, State.Disconnected)
                .Permit(Trigger.CallSucceeded, State.Connected)
                .OnEntry(this.OnUnreachable);

            this.machine.Configure(State.Disconnected)
                .Permit(Trigger.CallSucceeded, State.Connected)
                .InternalTransition(Trigger.CallTimedOut, () => this.ResetDisconnectedTimer())
                .OnEntry(this.OnDisconnected)
                .OnExit(this.OnDisconnectedExit);

            this.state = State.Trying;

            Events.Created(minConnectivityCheckFrequency, disconnectedCheckFrequency);
        }

        void ResetDisconnectedTimer()
        {
            this.disconnectedTimer.Stop();
            this.disconnectedTimer.Start();
        }

        void ResetConnectedTimer()
        {
            this.connectedTimer.Stop();
            this.connectedTimer.Start();
        }

        public void SetTestCloudProxy(ICloudProxy testClient)
        {
            this.connectivityChecker = new ConnectivityChecker(testClient);
            this.connectedTimer.Start();
            Events.SetTestCloudProxy();
        }

        public void CallSucceeded()
        {
            Events.CallSucceeded();
            this.machine.Fire(Trigger.CallSucceeded);
        }

        public void CallTimedOut()
        {
            Events.CallTimedOut();
            this.machine.Fire(Trigger.CallTimedOut);
        }

        void OnConnected()
        {
            Events.OnConnected();
            this.DeviceConnected?.Invoke(this, EventArgs.Empty);
            this.connectedTimer.Start();
        }

        void OnConnectedExit()
        {
            Events.OnConnectedExit();
            this.connectedTimer.Stop();
        }

        void OnUnreachable()
        {
            Events.OnUnreachable();
            this.CheckConnectivity();
        }

        void OnDisconnected()
        {
            Events.OnDisconnected();
            this.DeviceDisconnected?.Invoke(this, EventArgs.Empty);
            this.disconnectedTimer.Start();
        }

        void OnDisconnectedExit()
        {
            Events.OnDisconnectedExit();
            this.disconnectedTimer.Stop();
        }

        async void CheckConnectivity()
        {
            try
            {
                Events.MakingTestIotHubCall();
                await (this.connectivityChecker?.Check() ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                if (!(ex is TimeoutException))
                {
                    Events.ErrorCallingIotHub(ex);
                }
            }
        }

        class ConnectivityChecker
        {
            readonly ICloudProxy client;
            readonly AsyncLock sync = new AsyncLock();
            readonly Lazy<IMessage> testMessage = new Lazy<IMessage>(
                () =>
                {
                    string twinCollectionString = JsonConvert.SerializeObject(new TwinCollection());
                    return new EdgeMessage.Builder(Encoding.UTF8.GetBytes(twinCollectionString)).Build();
                });

            public ConnectivityChecker(ICloudProxy client)
            {
                this.client = Preconditions.CheckNotNull(client, nameof(client));
            }

            public async Task Check()
            {
                using (await this.sync.LockAsync())
                {
                    await this.client.UpdateReportedPropertiesAsync(this.testMessage.Value);
                }
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceConnectivityManager>();
            const int IdStart = CloudProxyEventIds.DeviceConnectivityManager;

            enum EventIds
            {
                Created = IdStart,
                CloudProxySet,
                CallTimedOut,
                OnDisconnectedExit,
                OnDisconnected,
                OnUnreachable,
                CallSucceeded,
                OnConnectedExit,
                OnConnected,
                ErrorCallingIotHub,
                MakingTestIotHubCall
            }

            internal static void SetTestCloudProxy()
            {
                Log.LogDebug((int)EventIds.CloudProxySet, Invariant($"CloudProxy to test device connectivity set"));
            }

            internal static void Created(TimeSpan connectedCheckFrequency, TimeSpan disconnectedCheckFrequency)
            {
                Log.LogDebug((int)EventIds.Created, Invariant($"Created DeviceConnectivityManager with connected check frequency {connectedCheckFrequency} and disconnected check frequency {disconnectedCheckFrequency}"));
            }

            public static void OnDisconnectedExit()
            {
                Log.LogInformation((int)EventIds.OnDisconnectedExit, Invariant($"Exiting disconnected state"));
            }

            public static void CallTimedOut()
            {
                Log.LogDebug((int)EventIds.CallTimedOut, Invariant($"IotHub call timed out"));
            }

            public static void OnDisconnected()
            {
                Log.LogInformation((int)EventIds.OnDisconnected, Invariant($"Entering disconnected state"));
            }

            public static void OnUnreachable()
            {
                Log.LogInformation((int)EventIds.OnUnreachable, Invariant($"Entering unreachable state"));
            }

            public static void CallSucceeded()
            {
                Log.LogDebug((int)EventIds.CallSucceeded, Invariant($"IotHub call succeeded"));
            }

            public static void OnConnected()
            {
                Log.LogInformation((int)EventIds.OnConnected, Invariant($"Entering connected state"));
            }

            public static void OnConnectedExit()
            {
                Log.LogInformation((int)EventIds.OnConnectedExit, Invariant($"Exiting connected state"));
            }

            public static void ErrorCallingIotHub(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorCallingIotHub, ex, Invariant($"Error calling IotHub for connectivity test"));
            }

            public static void MakingTestIotHubCall()
            {
                Log.LogDebug((int)EventIds.MakingTestIotHubCall, Invariant($"Calling IotHub to test connectivity"));
            }
        }
    }
}
