// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Gauge;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;
    using static System.FormattableString;
    using AsyncLock = Microsoft.Azure.Devices.Edge.Util.Concurrency.AsyncLock;

    public class ConnectionManager : IConnectionManager
    {
        const int DefaultMaxClients = 101; // 100 Clients + 1 Edgehub
        static readonly TimeSpan DefaultCloudConnectionRetryInterval = TimeSpan.FromSeconds(5);

        // Retry throttling must not be skewed by wall-clock jumps, so it is measured against a
        // process-wide monotonic clock.
        static readonly Stopwatch MonotonicClock = Stopwatch.StartNew();

        readonly object deviceConnLock = new object();
        readonly AsyncReaderWriterLock connectToCloudLock = new AsyncReaderWriterLock();
        readonly ConcurrentDictionary<string, ConnectedDevice> devices = new ConcurrentDictionary<string, ConnectedDevice>();
        readonly ICloudConnectionProvider cloudConnectionProvider;
        readonly int maxClients;
        readonly ICredentialsCache credentialsCache;
        readonly IIdentityProvider identityProvider;
        readonly IDeviceConnectivityManager connectivityManager;
        readonly bool closeCloudConnectionOnDeviceDisconnect;
        readonly TimeSpan cloudConnectionRetryInterval;
        readonly Func<TimeSpan> getMonotonicTime;

        public ConnectionManager(
            ICloudConnectionProvider cloudConnectionProvider,
            ICredentialsCache credentialsCache,
            IIdentityProvider identityProvider,
            IDeviceConnectivityManager connectivityManager,
            int maxClients = DefaultMaxClients,
            bool closeCloudConnectionOnDeviceDisconnect = true)
            : this(
                cloudConnectionProvider,
                credentialsCache,
                identityProvider,
                connectivityManager,
                maxClients,
                closeCloudConnectionOnDeviceDisconnect,
                DefaultCloudConnectionRetryInterval,
                () => MonotonicClock.Elapsed)
        {
        }

        internal ConnectionManager(
            ICloudConnectionProvider cloudConnectionProvider,
            ICredentialsCache credentialsCache,
            IIdentityProvider identityProvider,
            IDeviceConnectivityManager connectivityManager,
            int maxClients,
            bool closeCloudConnectionOnDeviceDisconnect,
            TimeSpan cloudConnectionRetryInterval,
            Func<TimeSpan> getMonotonicTime)
        {
            this.cloudConnectionProvider = Preconditions.CheckNotNull(cloudConnectionProvider, nameof(cloudConnectionProvider));
            this.maxClients = Preconditions.CheckRange(maxClients, 1, nameof(maxClients));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.identityProvider = Preconditions.CheckNotNull(identityProvider, nameof(identityProvider));
            this.connectivityManager = Preconditions.CheckNotNull(connectivityManager, nameof(connectivityManager));
            this.cloudConnectionRetryInterval = cloudConnectionRetryInterval >= TimeSpan.Zero
                ? cloudConnectionRetryInterval
                : throw new ArgumentOutOfRangeException(nameof(cloudConnectionRetryInterval));
            this.getMonotonicTime = Preconditions.CheckNotNull(getMonotonicTime, nameof(getMonotonicTime));
            this.connectivityManager.DeviceDisconnected += (o, args) => this.HandleDeviceCloudConnectionDisconnected();
            this.closeCloudConnectionOnDeviceDisconnect = closeCloudConnectionOnDeviceDisconnect;
        }

        public event EventHandler<IIdentity> CloudConnectionEstablished;

        public event EventHandler<IIdentity> CloudConnectionLost;

        public event EventHandler<IIdentity> DeviceConnected;

        public event EventHandler<IIdentity> DeviceDisconnected;

        public IEnumerable<IIdentity> GetConnectedClients() =>
            this.devices.Values
                .Where(d => d.DeviceConnection.Map(dc => dc.IsActive).GetOrElse(false))
                .Select(d => d.Identity);

        public async Task AddDeviceConnection(IIdentity identity, IDeviceProxy deviceProxy)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
            ConnectedDevice device = this.GetOrCreateConnectedDevice(identity);
            Option<DeviceConnection> currentDeviceConnection = device.AddDeviceConnection(deviceProxy);
            Events.NewDeviceConnection(identity);
            await currentDeviceConnection
                .Filter(dc => dc.IsActive)
                .ForEachAsync(dc => dc.CloseAsync(new MultipleConnectionsException($"Multiple connections detected for device {identity.Id}")));
            this.OnDeviceConnected(identity);
            this.DeviceConnected?.Invoke(this, identity);
        }

        public Task RemoveDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? this.RemoveDeviceConnection(device, removeCloudConnection: this.closeCloudConnectionOnDeviceDisconnect)
                : Task.CompletedTask;
        }

        public Option<IDeviceProxy> GetDeviceConnection(string id)
        {
            return this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.DeviceConnection.Filter(dp => dp.IsActive).Map(d => d.DeviceProxy)
                : Option.None<IDeviceProxy>();
        }

        public async Task<Option<ICloudProxy>> GetCloudConnection(string id)
        {
            Try<ICloudProxy> cloudProxyTry = await this.TryGetCloudConnectionInternal(id);
            return cloudProxyTry
                .Ok()
                .Map(c => (ICloudProxy)new RetryingCloudProxy(id, () => this.TryGetCloudConnectionInternal(id), c));
        }

        public async Task<Try<ICloudProxy>> TryGetCloudConnection(string id)
        {
            Try<ICloudProxy> cloudProxyTry = await this.TryGetCloudConnectionInternal(id);
            return cloudProxyTry.Success
                ? Try.Success((ICloudProxy)new RetryingCloudProxy(id, () => this.TryGetCloudConnectionInternal(id), cloudProxyTry.Value))
                : cloudProxyTry;
        }

        async Task<Try<ICloudProxy>> TryGetCloudConnectionInternal(string id)
        {
            IIdentity identity = this.identityProvider.Create(Preconditions.CheckNonWhiteSpace(id, nameof(id)));
            ConnectedDevice device = this.GetOrCreateConnectedDevice(identity);

            Try<ICloudConnection> cloudConnectionTry = await device.GetOrCreateCloudConnection(
                c => this.ConnectToCloud(c.Identity, this.CloudConnectionStatusChangedHandler));

            Events.GetCloudConnection(device.Identity, cloudConnectionTry);
            Try<ICloudProxy> cloudProxyTry = GetCloudProxyFromCloudConnection(cloudConnectionTry, device.Identity);
            return cloudProxyTry;
        }

        public bool AddSubscription(string id, DeviceSubscription deviceSubscription)
        {
            if (!this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device))
            {
                throw new ArgumentException($"A connection for {id} not found.");
            }

            // setting 'hasChanged' to false, so if no device connection, it doesn't indicate status change
            bool hasChanged = false;
            device.DeviceConnection.Filter(d => d.IsActive)
                .ForEach(d =>
                {
                    hasChanged = true; // if there is no old value, that means no subscription, so this is a change
                    d.Subscriptions.AddOrUpdate(
                        deviceSubscription,
                        true,
                        (_, old) =>
                        {
                            hasChanged = old != true;
                            return true;
                        });
                });

            return hasChanged;
        }

        public bool RemoveSubscription(string id, DeviceSubscription deviceSubscription)
        {
            if (!this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device))
            {
                throw new ArgumentException($"A connection for {id} not found.");
            }

            // setting 'hasChanged' to false, so if no device connection, it doesn't indicate status change
            bool hasChanged = false;
            device.DeviceConnection.Filter(d => d.IsActive)
                .ForEach(d =>
                {
                    hasChanged = false; // if there is no old value, that means no subscription, so this is not a change
                    d.Subscriptions.AddOrUpdate(
                        deviceSubscription,
                        false,
                        (_, old) =>
                        {
                            hasChanged = old != false;
                            return false;
                        });
                });

            return hasChanged;
        }

        public IReadOnlyCollection<DeviceSubscription> RemoveSubscriptions(string id)
        {
            if (!this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device))
            {
                throw new ArgumentException($"A connection for {id} not found.");
            }

            var toRemove = new List<DeviceSubscription>();
            device.DeviceConnection.Filter(d => d.IsActive)
                .ForEach(d =>
                {
                    foreach (var deviceSubscription in d.Subscriptions.Keys)
                    {
                        d.Subscriptions.AddOrUpdate(
                            deviceSubscription,
                            false,
                            (_, old) =>
                            {
                                if (old)
                                {
                                    toRemove.Add(deviceSubscription);
                                }

                                return false;
                            });
                    }
                });

            return toRemove;
        }

        public Option<IReadOnlyDictionary<DeviceSubscription, bool>> GetSubscriptions(string id) =>
            this.devices.TryGetValue(Preconditions.CheckNonWhiteSpace(id, nameof(id)), out ConnectedDevice device)
                ? device.DeviceConnection.Filter(d => d.IsActive)
                    .Map(d => new ReadOnlyDictionary<DeviceSubscription, bool>(d.Subscriptions) as IReadOnlyDictionary<DeviceSubscription, bool>)
                : Option.None<IReadOnlyDictionary<DeviceSubscription, bool>>();

        public bool CheckClientSubscription(string id, DeviceSubscription subscription) =>
            this.GetSubscriptions(id)
                .Filter(s => s.TryGetValue(subscription, out bool isActive) && isActive)
                .HasValue;

        public async Task<Try<ICloudProxy>> CreateCloudConnectionAsync(IClientCredentials credentials)
        {
            Preconditions.CheckNotNull(credentials, nameof(credentials));

            ConnectedDevice device = this.CreateOrUpdateConnectedDevice(credentials.Identity);
            Try<ICloudConnection> newCloudConnection = await device.CreateOrUpdateCloudConnection(c => this.CreateOrUpdateCloudConnection(c, credentials));
            Events.NewCloudConnection(credentials.Identity, newCloudConnection);
            Try<ICloudProxy> cloudProxyTry = GetCloudProxyFromCloudConnection(newCloudConnection, credentials.Identity);
            return cloudProxyTry.Success
                ? Try.Success((ICloudProxy)new RetryingCloudProxy(credentials.Identity.Id, () => this.TryGetCloudConnectionInternal(credentials.Identity.Id), cloudProxyTry.Value))
                : cloudProxyTry;
        }

        // This method is not used, but it has important logic and this will be useful for offline scenarios.
        // So do not delete this method.
        public async Task<Try<ICloudProxy>> GetOrCreateCloudConnectionAsync(IClientCredentials credentials)
        {
            Preconditions.CheckNotNull(credentials, nameof(credentials));

            // Get an existing ConnectedDevice from this.devices or add a new non-connected
            // instance to this.devices and return that.
            ConnectedDevice device = this.GetOrCreateConnectedDevice(credentials.Identity);

            Try<ICloudConnection> cloudConnectionTry = await device.GetOrCreateCloudConnection(
                c => this.CreateOrUpdateCloudConnection(c, credentials));
            Events.GetCloudConnection(credentials.Identity, cloudConnectionTry);
            Try<ICloudProxy> cloudProxyTry = GetCloudProxyFromCloudConnection(cloudConnectionTry, credentials.Identity);
            return cloudProxyTry.Success
                ? Try.Success((ICloudProxy)new RetryingCloudProxy(credentials.Identity.Id, () => this.TryGetCloudConnectionInternal(credentials.Identity.Id), cloudProxyTry.Value))
                : cloudProxyTry;
        }

        static Try<ICloudProxy> GetCloudProxyFromCloudConnection(Try<ICloudConnection> cloudConnection, IIdentity identity) => cloudConnection.Success
            ? cloudConnection.Value.CloudProxy.Map(Try.Success)
                .GetOrElse(() => Try<ICloudProxy>.Failure(new EdgeHubConnectionException($"Unable to get cloud proxy for device {identity.Id}")))
            : Try<ICloudProxy>.Failure(cloudConnection.Exception);

        async Task RemoveDeviceConnection(
            ConnectedDevice device,
            bool removeCloudConnection,
            bool throttleReconnect = false)
        {
            var id = device.Identity.Id;
            Events.RemovingDeviceConnection(id, removeCloudConnection);
            await device.DeviceConnection.Filter(dp => dp.IsActive)
                .ForEachAsync(dp => dp.CloseAsync(new EdgeHubConnectionException($"Connection closed for device {id}.")));

            if (removeCloudConnection)
            {
                await device.RemoveCloudConnection(
                    throttleReconnect: throttleReconnect,
                    preserveConnection: throttleReconnect);
            }

            Events.RemoveDeviceConnection(id);
            this.OnDeviceDisconnected(device.Identity);
            this.DeviceDisconnected?.Invoke(this, device.Identity);
        }

        Task<Try<ICloudConnection>> CreateOrUpdateCloudConnection(ConnectedDevice device, IClientCredentials credentials) =>
            device.CloudConnectionForUpdate.Map(
                    async c =>
                    {
                        try
                        {
                            if (!(credentials is ITokenCredentials tokenCredentials))
                            {
                                throw new InvalidOperationException($"Cannot update credentials of type {credentials.AuthenticationType} for {credentials.Identity.Id}");
                            }
                            else if (!(c is IClientTokenCloudConnection clientTokenCloudConnection))
                            {
                                throw new InvalidOperationException($"Cannot update token for an existing cloud connection that is not based on client token for {credentials.Identity.Id}");
                            }
                            else
                            {
                                await clientTokenCloudConnection.UpdateTokenAsync(tokenCredentials);
                                return Try.Success(c);
                            }
                        }
                        catch (Exception ex)
                        {
                            return Try<ICloudConnection>.Failure(new EdgeHubConnectionException($"Error updating identity for device {device.Identity.Id}", ex));
                        }
                    })
                .GetOrElse(() => this.ConnectToCloud(credentials, this.CloudConnectionStatusChangedHandler));

        async void CloudConnectionStatusChangedHandler(
            string deviceId,
            CloudConnectionStatus connectionStatus)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Events.HandlingConnectionStatusChangedHandler(deviceId, connectionStatus);
            if (!this.devices.TryGetValue(deviceId, out ConnectedDevice device))
            {
                throw new InvalidOperationException($"Device {deviceId} not found in the list of connected devices");
            }

            switch (connectionStatus)
            {
                case CloudConnectionStatus.TokenNearExpiry:
                    Events.ProcessingTokenNearExpiryEvent(device.Identity);
                    Option<IClientCredentials> clientCredentials = await this.credentialsCache.Get(device.Identity);
                    if (clientCredentials.HasValue)
                    {
                        await clientCredentials.ForEachAsync(
                            async cc =>
                            {
                                if (cc is ITokenCredentials tokenCredentials && tokenCredentials.IsUpdatable)
                                {
                                    Try<ICloudConnection> cloudConnectionTry = await device.CreateOrUpdateCloudConnection(c => this.CreateOrUpdateCloudConnection(c, tokenCredentials));
                                    if (!cloudConnectionTry.Success)
                                    {
                                        await this.RemoveDeviceConnection(device, removeCloudConnection: true, throttleReconnect: true);
                                        this.CloudConnectionLost?.Invoke(this, device.Identity);
                                    }
                                }
                                else
                                {
                                    await this.RemoveDeviceConnection(device, removeCloudConnection: this.closeCloudConnectionOnDeviceDisconnect);
                                }
                            });
                    }
                    else
                    {
                        await this.RemoveDeviceConnection(device, removeCloudConnection: true, throttleReconnect: true);
                        this.CloudConnectionLost?.Invoke(this, device.Identity);
                    }

                    break;

                case CloudConnectionStatus.DisconnectedTokenExpired:
                    await this.RemoveDeviceConnection(device, removeCloudConnection: true, throttleReconnect: true);
                    Events.InvokingCloudConnectionLostEvent(device.Identity);
                    this.CloudConnectionLost?.Invoke(this, device.Identity);
                    break;

                case CloudConnectionStatus.Disconnected:
                    Events.InvokingCloudConnectionLostEvent(device.Identity);
                    this.CloudConnectionLost?.Invoke(this, device.Identity);
                    break;

                case CloudConnectionStatus.ConnectionEstablished:
                    Events.InvokingCloudConnectionEstablishedEvent(device.Identity);
                    this.CloudConnectionEstablished?.Invoke(this, device.Identity);
                    break;
            }
        }

        async void HandleDeviceCloudConnectionDisconnected()
        {
            KeyValuePair<string, ConnectedDevice>[] snapshot;
            using (await this.connectToCloudLock.WriterLockAsync())
            {
                snapshot = this.devices.ToArray();
                Events.CloudConnectionLostClosingAllClients();
                foreach (var item in snapshot)
                {
                    if (item.Value.CloudConnection.Filter(cp => cp.IsActive).HasValue)
                    {
                        Events.CloudConnectionLostClosingClient(item.Value.Identity);
                    }

                    await item.Value.RemoveCloudConnection(
                        throttleReconnect: false,
                        preserveConnection: false);
                }
            }
        }

        ConnectedDevice GetOrCreateConnectedDevice(IIdentity identity)
        {
            string deviceId = Preconditions.CheckNotNull(identity, nameof(identity)).Id;
            return this.devices.GetOrAdd(
                Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId)),
                id => this.CreateNewConnectedDevice(identity));
        }

        ConnectedDevice CreateOrUpdateConnectedDevice(IIdentity identity)
        {
            string deviceId = Preconditions.CheckNotNull(identity, nameof(identity)).Id;
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            return this.devices.AddOrUpdate(
                deviceId,
                id => this.CreateNewConnectedDevice(identity),
                (id, cd) =>
                {
                    cd.UpdateIdentity(identity);
                    return cd;
                });
        }

        ConnectedDevice CreateNewConnectedDevice(IIdentity identity)
        {
            lock (this.deviceConnLock)
            {
                if (this.devices.Values.Count(d => d.DeviceConnection.Filter(d1 => d1.IsActive).HasValue) >= this.maxClients)
                {
                    throw new EdgeHubConnectionException($"Edge hub already has maximum allowed clients ({this.maxClients - 1}) connected.");
                }

                return new ConnectedDevice(identity, this.cloudConnectionRetryInterval, this.getMonotonicTime);
            }
        }

        async Task<Try<ICloudConnection>> ConnectToCloud(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            using (await this.connectToCloudLock.ReaderLockAsync())
            {
                return await this.cloudConnectionProvider.Connect(identity, connectionStatusChangedHandler);
            }
        }

        async Task<Try<ICloudConnection>> ConnectToCloud(IClientCredentials credentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            using (await this.connectToCloudLock.ReaderLockAsync())
            {
                return await this.cloudConnectionProvider.Connect(credentials, connectionStatusChangedHandler);
            }
        }

        class ConnectedDevice
        {
            // Generation parity is the removal flag: even means the cloud connection state is stable,
            // odd means a removal is in progress. A connection attempt captures the generation it
            // started under and may only publish its result while that generation is still current,
            // so any interleaving removal discards it.
            const long RemovalInProgressBit = 1;

            // Device Proxy methods are sync coming from the Protocol gateway,
            // so using traditional locking mechanism for those.
            readonly object deviceProxyLock = new object();
            readonly object cloudConnectionStateLock = new object();
            readonly AsyncLock cloudConnectionLock = new AsyncLock();
            readonly AsyncLock cloudConnectionRemovalLock = new AsyncLock();
            readonly TimeSpan cloudConnectionRetryInterval;
            readonly Func<TimeSpan> getMonotonicTime;
            ICloudConnection cloudConnection;
            ICloudConnection preservedCloudConnection;
            DeviceConnection deviceConnection;
            IIdentity identity;
            Task<Try<ICloudConnection>> cloudConnectionCreateTask;
            long cloudConnectionCreateGeneration;
            long cloudConnectionGeneration;
            TimeSpan? cloudConnectionCreateCompletedTime;
            bool shouldThrottleCloudConnectionCreation;

            public ConnectedDevice(IIdentity identity, TimeSpan cloudConnectionRetryInterval, Func<TimeSpan> getMonotonicTime)
            {
                this.identity = identity;
                this.cloudConnectionRetryInterval = cloudConnectionRetryInterval;
                this.getMonotonicTime = getMonotonicTime;
            }

            public IIdentity Identity => Volatile.Read(ref this.identity);

            public Option<ICloudConnection> CloudConnection
            {
                get
                {
                    ICloudConnection currentCloudConnection = Volatile.Read(ref this.cloudConnection);
                    return currentCloudConnection != null
                        ? Option.Some(currentCloudConnection)
                        : Option.None<ICloudConnection>();
                }
            }

            public Option<ICloudConnection> CloudConnectionForUpdate
            {
                get
                {
                    ICloudConnection currentCloudConnection = Volatile.Read(ref this.cloudConnection)
                        ?? Volatile.Read(ref this.preservedCloudConnection);
                    return currentCloudConnection != null
                        ? Option.Some(currentCloudConnection)
                        : Option.None<ICloudConnection>();
                }
            }

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public Option<DeviceConnection> DeviceConnection
            {
                get
                {
                    DeviceConnection currentDeviceConnection = Volatile.Read(ref this.deviceConnection);
                    return currentDeviceConnection != null
                        ? Option.Some(currentDeviceConnection)
                        : Option.None<DeviceConnection>();
                }
            }

            public void UpdateIdentity(IIdentity identity)
            {
                Volatile.Write(ref this.identity, Preconditions.CheckNotNull(identity, nameof(identity)));
            }

            public Option<DeviceConnection> AddDeviceConnection(IDeviceProxy deviceProxy)
            {
                Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
                lock (this.deviceProxyLock)
                {
                    DeviceConnection newDeviceConnection = new DeviceConnection(
                        deviceProxy,
                        new ConcurrentDictionary<DeviceSubscription, bool>());
                    DeviceConnection currentDeviceConnection = Interlocked.Exchange(
                        ref this.deviceConnection,
                        newDeviceConnection);
                    return currentDeviceConnection != null
                        ? Option.Some(currentDeviceConnection)
                        : Option.None<DeviceConnection>();
                }
            }

            public async Task<Try<ICloudConnection>> CreateOrUpdateCloudConnection(
                Func<ConnectedDevice, Task<Try<ICloudConnection>>> cloudConnectionUpdater)
            {
                Preconditions.CheckNotNull(cloudConnectionUpdater, nameof(cloudConnectionUpdater));
                await this.WaitForPendingCloudConnectionRemoval();
                // Lock in case multiple connections are created to the cloud for the same device at the same time
                using (await this.cloudConnectionLock.LockAsync())
                {
                    long connectionGeneration = Volatile.Read(ref this.cloudConnectionGeneration);
                    Try<ICloudConnection> newCloudConnection = await cloudConnectionUpdater(this);
                    bool invalidated;
                    lock (this.cloudConnectionStateLock)
                    {
                        invalidated = !this.IsCloudConnectionGenerationValid(connectionGeneration);
                        if (!invalidated)
                        {
                            if (newCloudConnection.Success)
                            {
                                Volatile.Write(ref this.cloudConnection, newCloudConnection.Value);
                                Interlocked.CompareExchange(
                                    ref this.preservedCloudConnection,
                                    null,
                                    newCloudConnection.Value);
                            }

                            if (newCloudConnection.Success && newCloudConnection.Value.IsActive)
                            {
                                this.ResetCloudConnectionRetryStateCore(connectionGeneration);
                            }
                            else
                            {
                                this.RecordFailedCloudConnectionAttemptCore(
                                    Task.FromResult(newCloudConnection),
                                    connectionGeneration);
                            }
                        }
                    }

                    if (invalidated)
                    {
                        return await this.DiscardInvalidatedCloudConnection(newCloudConnection);
                    }

                    return newCloudConnection;
                }
            }

            public async Task<Try<ICloudConnection>> GetOrCreateCloudConnection(
                Func<ConnectedDevice, Task<Try<ICloudConnection>>> cloudConnectionCreator)
            {
                Preconditions.CheckNotNull(cloudConnectionCreator, nameof(cloudConnectionCreator));

                long connectionGeneration = Volatile.Read(ref this.cloudConnectionGeneration);
                Task<Try<ICloudConnection>> createTask = Volatile.Read(ref this.cloudConnectionCreateTask);
                if (createTask != null
                    && !createTask.IsCompleted
                    && this.IsCloudConnectionGenerationValid(connectionGeneration)
                    && connectionGeneration == Volatile.Read(ref this.cloudConnectionCreateGeneration))
                {
                    return await createTask;
                }

                using (await this.cloudConnectionLock.LockAsync())
                {
                    lock (this.cloudConnectionStateLock)
                    {
                        connectionGeneration = Volatile.Read(ref this.cloudConnectionGeneration);
                        Option<ICloudConnection> activeCloudConnection = this.CloudConnection.Filter(cp => cp.IsActive);
                        if (this.IsCloudConnectionGenerationValid(connectionGeneration)
                            && activeCloudConnection.HasValue)
                        {
                            return Try.Success(activeCloudConnection.OrDefault());
                        }
                    }

                    TaskCompletionSource<Try<ICloudConnection>> createTaskSource = null;
                    bool replacesExistingConnection = false;
                    lock (this.cloudConnectionStateLock)
                    {
                        connectionGeneration = Volatile.Read(ref this.cloudConnectionGeneration);
                        if (!this.IsCloudConnectionGenerationValid(connectionGeneration))
                        {
                            return Try<ICloudConnection>.Failure(
                                new EdgeHubConnectionException($"Cloud connection for device {this.Identity.Id} is being removed."));
                        }

                        bool reuseCreateTask = false;
                        createTask = this.cloudConnectionCreateTask;
                        if (createTask != null && connectionGeneration == this.cloudConnectionCreateGeneration)
                        {
                            reuseCreateTask = !createTask.IsCompleted
                                || (this.shouldThrottleCloudConnectionCreation
                                    && !this.CloudConnectionRetryIntervalElapsed());
                            if (reuseCreateTask && createTask.IsCompleted)
                            {
                                Events.ReusingRecentCloudConnectionAttempt(this.Identity, this.cloudConnectionRetryInterval);
                            }
                        }

                        if (!reuseCreateTask)
                        {
                            replacesExistingConnection = this.CloudConnectionForUpdate.HasValue;
                            // Publish the placeholder under the lock so concurrent callers join this attempt,
                            // but run the caller-supplied creator only after the lock is released.
                            createTaskSource = new TaskCompletionSource<Try<ICloudConnection>>(
                                TaskCreationOptions.RunContinuationsAsynchronously);
                            createTask = createTaskSource.Task;
                            Volatile.Write(ref this.cloudConnectionCreateGeneration, connectionGeneration);
                            Volatile.Write(ref this.cloudConnectionCreateTask, createTask);
                        }
                    }

                    if (createTaskSource != null)
                    {
                        try
                        {
                            createTaskSource.SetResult(
                                await this.CreateCloudConnection(
                                    cloudConnectionCreator,
                                    replacesExistingConnection,
                                    connectionGeneration));
                        }
                        catch (Exception ex)
                        {
                            createTaskSource.SetException(ex);
                        }
                    }

                    return await createTask;
                }
            }

            async Task<Try<ICloudConnection>> CreateCloudConnection(
                Func<ConnectedDevice, Task<Try<ICloudConnection>>> cloudConnectionCreator,
                bool replacesExistingConnection,
                long connectionGeneration)
            {
                Task<Try<ICloudConnection>> createTask = null;
                try
                {
                    createTask = cloudConnectionCreator(this);
                    Try<ICloudConnection> cloudConnectionResult = await createTask;
                    bool invalidated;
                    ICloudConnection displacedPreservedConnection = null;
                    lock (this.cloudConnectionStateLock)
                    {
                        invalidated = !this.IsCloudConnectionGenerationValid(connectionGeneration);
                        if (!invalidated)
                        {
                            Volatile.Write(
                                ref this.cloudConnection,
                                cloudConnectionResult.Success ? cloudConnectionResult.Value : null);
                            if (cloudConnectionResult.Success)
                            {
                                displacedPreservedConnection = Interlocked.Exchange(
                                    ref this.preservedCloudConnection,
                                    null);
                            }

                            this.shouldThrottleCloudConnectionCreation = replacesExistingConnection
                                || !cloudConnectionResult.Success
                                || !cloudConnectionResult.Value.IsActive;
                        }
                    }

                    if (invalidated)
                    {
                        return await this.DiscardInvalidatedCloudConnection(cloudConnectionResult);
                    }

                    if (displacedPreservedConnection != null
                        && !ReferenceEquals(displacedPreservedConnection, cloudConnectionResult.Value))
                    {
                        await CancelTokenUpdateAndCloseAsync(displacedPreservedConnection);
                    }

                    return cloudConnectionResult;
                }
                finally
                {
                    lock (this.cloudConnectionStateLock)
                    {
                        if (this.IsCloudConnectionGenerationValid(connectionGeneration))
                        {
                            this.cloudConnectionCreateCompletedTime = this.getMonotonicTime();
                            if (createTask == null || createTask.IsFaulted || createTask.IsCanceled)
                            {
                                this.shouldThrottleCloudConnectionCreation = true;
                            }
                        }
                    }
                }
            }

            // Barrier: acquiring and immediately releasing the removal lock guarantees that a removal
            // already in flight has published its final generation and throttle state before the caller
            // reads them. The lock is deliberately not held across the caller's work, because a cloud
            // callback raised during that work can itself trigger a removal and would deadlock on it.
            async Task WaitForPendingCloudConnectionRemoval()
            {
                using (await this.cloudConnectionRemovalLock.LockAsync())
                {
                }
            }

            bool CloudConnectionRetryIntervalElapsed()
            {
                if (!this.cloudConnectionCreateCompletedTime.HasValue)
                {
                    return true;
                }

                TimeSpan elapsed = this.getMonotonicTime() - this.cloudConnectionCreateCompletedTime.Value;
                return elapsed < TimeSpan.Zero || elapsed >= this.cloudConnectionRetryInterval;
            }

            void RecordFailedCloudConnectionAttemptCore(
                Task<Try<ICloudConnection>> createTask,
                long connectionGeneration)
            {
                if (!this.IsCloudConnectionGenerationValid(connectionGeneration))
                {
                    return;
                }

                Volatile.Write(ref this.cloudConnectionCreateGeneration, connectionGeneration);
                Volatile.Write(ref this.cloudConnectionCreateTask, createTask);
                this.cloudConnectionCreateCompletedTime = this.getMonotonicTime();
                this.shouldThrottleCloudConnectionCreation = true;
            }

            void ResetCloudConnectionRetryStateCore(long connectionGeneration)
            {
                if (!this.IsCloudConnectionGenerationValid(connectionGeneration))
                {
                    return;
                }

                Volatile.Write(ref this.cloudConnectionCreateGeneration, connectionGeneration);
                Volatile.Write(ref this.cloudConnectionCreateTask, null);
                this.cloudConnectionCreateCompletedTime = null;
                this.shouldThrottleCloudConnectionCreation = false;
            }

            public async Task RemoveCloudConnection(
                bool throttleReconnect,
                bool preserveConnection)
            {
                using (await this.cloudConnectionRemovalLock.LockAsync())
                {
                    var removedCloudConnections = new List<ICloudConnection>(2);
                    var supersededCloudConnections = new List<ICloudConnection>(1);
                    lock (this.cloudConnectionStateLock)
                    {
                        this.BeginCloudConnectionRemoval();
                        ICloudConnection activeCloudConnection =
                            Interlocked.Exchange(ref this.cloudConnection, null);
                        if (activeCloudConnection != null)
                        {
                            removedCloudConnections.Add(activeCloudConnection);
                        }

                        if (preserveConnection)
                        {
                            if (activeCloudConnection != null)
                            {
                                ICloudConnection previousPreservedConnection =
                                    Interlocked.Exchange(
                                        ref this.preservedCloudConnection,
                                        activeCloudConnection);
                                if (previousPreservedConnection != null
                                    && !ReferenceEquals(previousPreservedConnection, activeCloudConnection))
                                {
                                    supersededCloudConnections.Add(previousPreservedConnection);
                                }
                            }
                        }
                        else
                        {
                            ICloudConnection preservedConnection =
                                Interlocked.Exchange(ref this.preservedCloudConnection, null);
                            if (preservedConnection != null
                                && !ReferenceEquals(preservedConnection, activeCloudConnection))
                            {
                                removedCloudConnections.Add(preservedConnection);
                            }
                        }
                    }

                    try
                    {
                        foreach (ICloudConnection supersededCloudConnection in supersededCloudConnections)
                        {
                            if (supersededCloudConnection is IClientTokenCloudConnection clientTokenCloudConnection)
                            {
                                clientTokenCloudConnection.CancelTokenUpdate();
                            }

                            if (supersededCloudConnection.IsActive)
                            {
                                await supersededCloudConnection.CloseAsync();
                            }
                        }

                        foreach (ICloudConnection removedCloudConnection in removedCloudConnections)
                        {
                            bool preserveForTokenUpdate = false;
                            if (removedCloudConnection is IClientTokenCloudConnection clientTokenCloudConnection)
                            {
                                if (!preserveConnection)
                                {
                                    clientTokenCloudConnection.CancelTokenUpdate();
                                }
                                else if (clientTokenCloudConnection.HasPendingTokenUpdate)
                                {
                                    preserveForTokenUpdate = true;
                                }
                                else
                                {
                                    clientTokenCloudConnection.CancelTokenUpdate();
                                }
                            }

                            if (preserveForTokenUpdate)
                            {
                                continue;
                            }

                            if (preserveConnection)
                            {
                                Interlocked.CompareExchange(
                                    ref this.preservedCloudConnection,
                                    null,
                                    removedCloudConnection);
                            }

                            if (removedCloudConnection.IsActive)
                            {
                                await removedCloudConnection.CloseAsync();
                            }
                        }
                    }
                    finally
                    {
                        lock (this.cloudConnectionStateLock)
                        {
                            long stableGeneration = this.NextStableCloudConnectionGeneration();
                            Volatile.Write(ref this.cloudConnectionCreateGeneration, stableGeneration);
                            this.cloudConnectionCreateCompletedTime = throttleReconnect
                                ? this.getMonotonicTime()
                                : (TimeSpan?)null;
                            this.shouldThrottleCloudConnectionCreation = throttleReconnect;
                            Volatile.Write(
                                ref this.cloudConnectionCreateTask,
                                throttleReconnect
                                    ? Task.FromResult(
                                        Try<ICloudConnection>.Failure(
                                            new EdgeHubConnectionException(
                                                $"Cloud connection for device {this.Identity.Id} was removed after a cloud failure.")))
                                    : null);
                            this.CompleteCloudConnectionRemoval();
                        }
                    }
                }
            }

            static bool IsStableGeneration(long generation) => (generation & RemovalInProgressBit) == 0;

            // Cancelling first is what makes the close final: an in-flight token update would otherwise
            // hand back a live client for a connection the caller has already discarded.
            static async Task CancelTokenUpdateAndCloseAsync(ICloudConnection cloudConnection)
            {
                if (cloudConnection is IClientTokenCloudConnection clientTokenCloudConnection)
                {
                    clientTokenCloudConnection.CancelTokenUpdate();
                }

                await cloudConnection.CloseAsync();
            }

            void BeginCloudConnectionRemoval() => Interlocked.Increment(ref this.cloudConnectionGeneration);

            void CompleteCloudConnectionRemoval() => Interlocked.Increment(ref this.cloudConnectionGeneration);

            // Only valid while a removal is in progress: the generation is odd, so the next increment
            // restores the stable generation that post-removal connection attempts will run under.
            long NextStableCloudConnectionGeneration() => Volatile.Read(ref this.cloudConnectionGeneration) + 1;

            bool IsCloudConnectionGenerationValid(long connectionGeneration) =>
                IsStableGeneration(connectionGeneration)
                && connectionGeneration == Volatile.Read(ref this.cloudConnectionGeneration);

            async Task<Try<ICloudConnection>> DiscardInvalidatedCloudConnection(
                Try<ICloudConnection> cloudConnection)
            {
                if (cloudConnection.Success)
                {
                    Interlocked.CompareExchange(
                        ref this.cloudConnection,
                        null,
                        cloudConnection.Value);
                    Interlocked.CompareExchange(
                        ref this.preservedCloudConnection,
                        null,
                        cloudConnection.Value);
                    await CancelTokenUpdateAndCloseAsync(cloudConnection.Value);
                }

                return Try<ICloudConnection>.Failure(
                    new EdgeHubConnectionException($"Cloud connection attempt for device {this.Identity.Id} was invalidated."));
            }
        }

        class DeviceConnection
        {
            public DeviceConnection(IDeviceProxy deviceProxy, ConcurrentDictionary<DeviceSubscription, bool> subscriptions)
            {
                this.Subscriptions = subscriptions;
                this.DeviceProxy = deviceProxy;
            }

            public IDeviceProxy DeviceProxy { get; }

            public ConcurrentDictionary<DeviceSubscription, bool> Subscriptions { get; }

            public bool IsActive => this.DeviceProxy.IsActive;

            public Task CloseAsync(Exception ex) => this.DeviceProxy.CloseAsync(ex);
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.ConnectionManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionManager>();

            enum EventIds
            {
                CreateNewCloudConnection = IdStart,
                NewDeviceConnection,
                RemovingDeviceConnection,
                RemoveDeviceConnection,
                CreateNewCloudConnectionError,
                ObtainedCloudConnection,
                ObtainCloudConnectionError,
                ProcessingTokenNearExpiryEvent,
                InvokingCloudConnectionLostEvent,
                InvokingCloudConnectionEstablishedEvent,
                HandlingConnectionStatusChangedHandler,
                CloudConnectionLostClosingClient,
                CloudConnectionLostClosingAllClients,
                GettingCloudConnectionForDeviceSubscriptions,
                ReusingRecentCloudConnectionAttempt
            }

            public static void NewCloudConnection(IIdentity identity, Try<ICloudConnection> cloudConnection)
            {
                if (cloudConnection.Success)
                {
                    Log.LogInformation((int)EventIds.CreateNewCloudConnection, Invariant($"New cloud connection created for device {identity.Id}"));
                }
                else
                {
                    Log.LogInformation((int)EventIds.CreateNewCloudConnectionError, cloudConnection.Exception, Invariant($"Error creating new device connection for device {identity.Id}"));
                }
            }

            public static void NewDeviceConnection(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.NewDeviceConnection, Invariant($"New device connection for device {identity.Id}"));
            }

            public static void RemovingDeviceConnection(string id, bool removeCloudConnection)
            {
                Log.LogInformation((int)EventIds.RemovingDeviceConnection, Invariant($"Removing device connection for device {id} with removeCloudConnection flag '{removeCloudConnection}'."));
            }

            public static void RemoveDeviceConnection(string id)
            {
                Log.LogInformation((int)EventIds.RemoveDeviceConnection, Invariant($"Device connection removed for device {id}"));
            }

            public static void ProcessingTokenNearExpiryEvent(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.ProcessingTokenNearExpiryEvent, Invariant($"Processing token near expiry for {identity.Id}"));
            }

            public static void InvokingCloudConnectionLostEvent(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.InvokingCloudConnectionLostEvent, Invariant($"Invoking cloud connection lost event for {identity.Id}"));
            }

            public static void InvokingCloudConnectionEstablishedEvent(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.InvokingCloudConnectionEstablishedEvent, Invariant($"Invoking cloud connection established event for {identity.Id}"));
            }

            public static void HandlingConnectionStatusChangedHandler(string deviceId, CloudConnectionStatus connectionStatus)
            {
                Log.LogInformation((int)EventIds.HandlingConnectionStatusChangedHandler, Invariant($"Connection status for {deviceId} changed to {connectionStatus}"));
            }

            public static void CloudConnectionLostClosingClient(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.CloudConnectionLostClosingClient, Invariant($"Cloud connection lost for {identity.Id}, closing client."));
            }

            internal static void GetCloudConnection(IIdentity identity, Try<ICloudConnection> cloudConnection)
            {
                if (cloudConnection.Success)
                {
                    Log.LogDebug((int)EventIds.ObtainedCloudConnection, Invariant($"Obtained cloud connection for device {identity.Id}"));
                }
                else
                {
                    Log.LogInformation((int)EventIds.ObtainCloudConnectionError, cloudConnection.Exception, Invariant($"Error getting cloud connection for device {identity.Id}"));
                }
            }

            public static void CloudConnectionLostClosingAllClients()
            {
                Log.LogDebug((int)EventIds.CloudConnectionLostClosingAllClients, Invariant($"Cloud connection lost, closing all clients."));
            }

            public static void GettingCloudConnectionForDeviceSubscriptions()
            {
                Log.LogDebug((int)EventIds.GettingCloudConnectionForDeviceSubscriptions, $"Device has subscriptions. Trying to get cloud connection.");
            }

            public static void ReusingRecentCloudConnectionAttempt(IIdentity identity, TimeSpan retryInterval)
            {
                Log.LogDebug(
                    (int)EventIds.ReusingRecentCloudConnectionAttempt,
                    Invariant($"Reusing recent cloud connection attempt for device {identity.Id} during {retryInterval} retry interval."));
            }
        }

        static class MetricsV0
        {
            static readonly GaugeOptions ConnectedClientGaugeOptions = new GaugeOptions
            {
                Name = "EdgeHubConnectedClientGauge",
                MeasurementUnit = Unit.Events
            };

            public static void SetConnectedClientCountGauge(ConnectionManager connectionManager)
            {
                // Subtract EdgeHub from the list of connected clients
                int connectedClients = connectionManager.GetConnectedClients().Count() - 1;
            }
        }

        void OnDeviceConnected(IIdentity identity)
        {
            DeviceConnectionMetrics.OnDeviceConnected(identity.ToString());
            DeviceConnectionMetrics.UpdateConnectedClients(this.GetConnectedClients().Count() - 1);
        }

        void OnDeviceDisconnected(IIdentity identity)
        {
            DeviceConnectionMetrics.OnDeviceDisconnected(identity.ToString());
            DeviceConnectionMetrics.UpdateConnectedClients(this.GetConnectedClients().Count() - 1);
        }
    }
}
