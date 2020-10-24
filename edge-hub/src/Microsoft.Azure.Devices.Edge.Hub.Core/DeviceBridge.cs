// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    // This class is a wrapper of device and cloud connection. It put them togather as a bundle and recover cloud connection once it's disconnected.
    internal class DeviceBridge
    {
        static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);
        readonly object stateLock = new object();
        readonly ICloudConnectionProvider cloudConnectionProvider;
        readonly bool closeCloudConnectionOnDeviceDisconnect;

        // subscriptions from downstream
        readonly ISet<DeviceSubscription> subscriptions = new HashSet<DeviceSubscription>();
        // connection from downstream
        IDeviceProxy deviceProxy;

        // connection to upstream
        Option<IClientCredentials> clientCredentials;
        ICloudProxy cloudProxy;
        Task<ITry<ICloudProxy>> createCloudProxyTask;
        Action<DeviceBridge, CloudConnectionStatus> onCloudConnectionStatusChanged;
        string cloudConnectionStatusChangedHandlerId;

        internal IIdentity Identity { get; }

        internal DeviceBridge(IIdentity identity, bool closeCloudConnectionOnDeviceDisconnect, ICloudConnectionProvider cloudConnectionProvider, Action<DeviceBridge, CloudConnectionStatus> onCloudConnectionStatusChanged)
        {
            this.Identity = identity;
            this.closeCloudConnectionOnDeviceDisconnect = closeCloudConnectionOnDeviceDisconnect;
            this.onCloudConnectionStatusChanged = onCloudConnectionStatusChanged;
            this.cloudConnectionProvider = cloudConnectionProvider;
            this.clientCredentials = Option.None<IClientCredentials>();
        }

        internal DeviceBridge(IClientCredentials clientCredentials, bool closeCloudConnectionOnDeviceDisconnect, ICloudConnectionProvider cloudConnectionProvider, Action<DeviceBridge, CloudConnectionStatus> onCloudConnectionStatusChanged)
        {
            this.Identity = clientCredentials.Identity;
            this.closeCloudConnectionOnDeviceDisconnect = closeCloudConnectionOnDeviceDisconnect;
            this.onCloudConnectionStatusChanged = onCloudConnectionStatusChanged;
            this.cloudConnectionProvider = cloudConnectionProvider;
            this.clientCredentials = Option.Some(clientCredentials);
        }

        internal IDeviceProxy GetDeviceProxy()
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                return this.deviceProxy != null && this.deviceProxy.IsActive ? this.deviceProxy : null;
            }
        }

        internal Task ReplaceDeviceProxyAsync(IDeviceProxy deviceProxy)
        {
            Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                Task closeDeviceProxyTask = this.deviceProxy == null ? Task.CompletedTask
                    : this.deviceProxy.CloseAsync(new MultipleConnectionsException($"Multiple connections detected for device {this.Identity}"));
                // TODO what about subscriptions???
                this.deviceProxy = deviceProxy;
                return closeDeviceProxyTask;
            }
        }

        internal Task CloseDeviceProxyAsync()
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                var closeDeviceProxyTask = this.deviceProxy == null ? Task.CompletedTask
                    : this.deviceProxy.CloseAsync(new EdgeHubConnectionException($"Connection closed for device {this.Identity}."));
                var closeCloudProxyTask = this.closeCloudConnectionOnDeviceDisconnect && this.cloudProxy != null ? this.cloudProxy.CloseAsync() : Task.CompletedTask;

                this.deviceProxy = null;
                this.cloudProxy = null;
                this.createCloudProxyTask = null;
                return Task.WhenAll(closeDeviceProxyTask, closeCloudProxyTask);
            }
        }

        internal Task CloseCloudProxyAsync()
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                var closeCloudProxyTask = this.cloudProxy == null ? Task.CompletedTask : this.cloudProxy.CloseAsync();
                this.cloudProxy = null;
                this.createCloudProxyTask = null;
                return closeCloudProxyTask;
            }
        }

        internal bool IsDeviceProxyActive()
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                return this.deviceProxy?.IsActive ?? false;
            }
        }

        internal void AddSubscription(DeviceSubscription deviceSubscription)
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                if (!this.deviceProxy?.IsActive ?? false)
                {
                    throw new ArgumentException($"DeviceProxy {this.Identity.Id} is inactive.");
                }

                this.subscriptions.Add(deviceSubscription);
            }
        }

        internal void RemoveSubscription(DeviceSubscription deviceSubscription)
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                if (!this.deviceProxy?.IsActive ?? false)
                {
                    throw new ArgumentException($"DeviceProxy {this.Identity.Id} is inactive.");
                }

                this.subscriptions.Remove(deviceSubscription);
            }
        }

        internal ISet<DeviceSubscription> GetSubscriptions()
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                return this.deviceProxy?.IsActive ?? false ? ImmutableHashSet.CreateRange(this.subscriptions) : null;
            }
        }

        internal bool CheckClientSubscription(DeviceSubscription subscription)
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                if (!this.deviceProxy?.IsActive ?? false)
                {
                    return false;
                }

                return this.subscriptions.Contains(subscription);
            }
        }

        internal async Task<ITry<ICloudProxy>> TryRetrieveCloudProxyAsync()
        {
            var newTask = false;
            Task<ITry<ICloudProxy>> task;
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                // if existing cloudProxy is active, reuse it
                if (this.cloudProxy?.IsActive ?? false)
                {
                    return Try.Success(this.cloudProxy);
                }

                // reuse existing createCloudProxyTask
                if (this.createCloudProxyTask != null)
                {
                    task = this.createCloudProxyTask;
                }
                else
                {
                    // if no createCloudProxyTask, create new createCloudProxyTask
                    task = this.TryCreateCloudProxyAsync();
                    this.createCloudProxyTask = task;
                    newTask = true;
                }
            }

            var result = await task;

            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                if (!newTask)
                {
                    // if reuse existing task, just return result
                    return result;
                }

                // double check if task is the same as existing createCloudProxyTask, return the result
                if (task == this.createCloudProxyTask)
                {
                    this.createCloudProxyTask = null;
                    if (result.Success)
                    {
                        this.cloudProxy = result.Value;
                    }

                    return result;
                }
            }

            // task is different from existing createCloudProxyTask, it means task was cancelled and we need to close created ICloudProxy.
            // Otherwise it will leak a DeviceClient instance and play pingpong
            if (result.Success)
            {
                await result.Value.CloseAsync();
            }

            return Try.Failure<ICloudProxy>(new OperationCanceledException("Operation is cancelled."));
        }

        internal async Task<Option<ICloudProxy>> RetrieveCloudProxyAsync() => (await this.TryRetrieveCloudProxyAsync()).ToOption();

        async Task<ITry<ICloudProxy>> TryCreateCloudProxyAsync()
        {
            Action<string, CloudConnectionStatus> cloudConnectionStatusChangedHandler;
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                // this id is used to make sure connection status change event is for existing clould connection to avoid race.
                var id = Guid.NewGuid().ToString();
                this.cloudConnectionStatusChangedHandlerId = id;
                cloudConnectionStatusChangedHandler = async (deviceId, status) =>
                {
                    await this.CloudConnectionStatusChangedHandler(id, deviceId, status);
                };
            }

            var cloudConnection = await this.clientCredentials.Map(cc => this.cloudConnectionProvider.Connect(cc, cloudConnectionStatusChangedHandler))
                .GetOrElse(() => this.cloudConnectionProvider.Connect(this.Identity, cloudConnectionStatusChangedHandler));
            return cloudConnection.Map(cc => cc.CloudProxy)
                .Map(cp => cp.Expect(() => new EdgeHubConnectionException($"Unable to get cloud proxy for device {this.Identity}")))
                .Map(cp => new RetryingCloudProxy(this.Identity.Id, this.TryCreateCloudProxyAsync, cp) as ICloudProxy);
        }

        async Task CloudConnectionStatusChangedHandler(
            string id,
            string deviceId,
            CloudConnectionStatus connectionStatus)
        {
            using (SyncLock.Lock(this.stateLock, OperationTimeout))
            {
                if (this.cloudConnectionStatusChangedHandlerId != id)
                {
                    // Cloud proxy status changed ignored since the handle id mismatch.
                    return;
                }
            }

            if (this.Identity.Id != deviceId)
            {
                throw new AggregateException($"DeviceBridge with {this.Identity} got a event {connectionStatus} for {deviceId}");
            }

            switch (connectionStatus)
            {
                case CloudConnectionStatus.DisconnectedTokenExpired:
                case CloudConnectionStatus.Disconnected:
                    await this.CloseCloudProxyAsync();
                    // try to recover, ignore if recovered
                    if (this.deviceProxy?.IsActive ?? false)
                    {
                        // only try to recover while device is connected
                        var cloudProxyTry = await this.TryRetrieveCloudProxyAsync();
                        if (!cloudProxyTry.Success)
                        {
                            // If reconnect failed,
                            // 1. For client credentials based device/module(not in scope), we are not able to check if the credentials is valid, drop device connection and let device retry
                            // 2. For device/module in scope, only chance it failed is either device/module state changed(scope change or disable) or network outage.
                            // 1.1 For former, drop device connection and let device retry.
                            // 1.2 For latter, connection should be able to re-esstablish while DeviceConnectivityManager trigger recover.
                            if (this.clientCredentials != null || IsCausedByInvalidDevice(cloudProxyTry.Exception))
                            {
                                await this.CloseDeviceProxyAsync();
                            }

                            this.onCloudConnectionStatusChanged(this, connectionStatus);
                        }
                    }
                    else
                    {
                        this.onCloudConnectionStatusChanged(this, connectionStatus);
                    }

                    break;
                default:
                    this.onCloudConnectionStatusChanged(this, connectionStatus);
                    break;
            }
        }

        static bool IsCausedByInvalidDevice(Exception ex)
        {
            return ex is DeviceNotFoundException || ex is UnauthorizedException || ex is AuthenticationException;
        }

        public class SyncLock : IDisposable
        {
            readonly object locker;

            SyncLock(object locker)
            {
                this.locker = locker;
            }

            public static SyncLock Lock(object locker, TimeSpan timeout)
            {
                if (Monitor.TryEnter(locker, timeout))
                    return new SyncLock(locker);
                else
                    throw new TimeoutException("Failed to acquire the lock.");
            }

            public void Dispose() => Monitor.Exit(this.locker);
        }
    }
}
