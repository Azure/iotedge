// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Timers;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public sealed class ConnectionReauthenticator : IDisposable
    {
        readonly IAuthenticator authenticator;

        readonly IConnectionManager connectionManager;

        readonly ICredentialsCache credentialsCache;

        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;

        readonly IIdentity edgeHubIdentity;

        readonly Timer timer;

        public ConnectionReauthenticator(
            IConnectionManager connectionManager,
            IAuthenticator authenticator,
            ICredentialsCache credentialsCache,
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            TimeSpan reauthenticateFrequency,
            IIdentity edgeHubIdentity)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
            this.edgeHubIdentity = Preconditions.CheckNotNull(edgeHubIdentity, nameof(edgeHubIdentity));
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
            this.timer = new Timer(reauthenticateFrequency.TotalMilliseconds);
            this.timer.Elapsed += this.ReauthenticateConnections;
            this.connectionManager.CloudConnectionEstablished += this.CloudConnectionEstablishedHandler;
            this.deviceScopeIdentitiesCache.ServiceIdentityUpdated += this.HandleServiceIdentityUpdate;
            this.deviceScopeIdentitiesCache.ServiceIdentityRemoved += this.HandleServiceIdentityRemove;
        }

        public void Dispose() => this.timer?.Dispose();

        public void Init()
        {
            Events.StartingReauthTimer(this.timer);
            this.timer.Start();
        }

        void CloudConnectionEstablishedHandler(object sender, IIdentity identity)
        {
            if (this.edgeHubIdentity.Id.Equals(identity.Id))
            {
                Events.EdgeHubConnectionReestablished();
                this.deviceScopeIdentitiesCache.InitiateCacheRefresh();
            }
        }

        async void HandleServiceIdentityRemove(object sender, string id)
        {
            try
            {
                if (this.IsEdgeHubIdentity(id))
                {
                    return;
                }

                Events.ServiceIdentityRemoved(id);
                await this.connectionManager.RemoveDeviceConnection(id);
            }
            catch (Exception ex)
            {
                Events.ErrorRemovingConnection(ex, id);
            }
        }

        async void HandleServiceIdentityUpdate(object sender, ServiceIdentity serviceIdentity)
        {
            try
            {
                Events.ServiceIdentityUpdated(serviceIdentity.Id);
                if (this.IsEdgeHubIdentity(serviceIdentity.Id))
                {
                    return;
                }

                Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(serviceIdentity.Id);
                if (deviceProxy.HasValue)
                {
                    await deviceProxy.ForEachAsync(
                        async dp =>
                        {
                            Option<IClientCredentials> clientCredentials = await this.credentialsCache.Get(dp.Identity);
                            await clientCredentials.ForEachAsync(
                                async c =>
                                {
                                    if (!await this.authenticator.ReauthenticateAsync(c))
                                    {
                                        Events.ServiceIdentityUpdatedRemoving(serviceIdentity.Id);
                                        await this.connectionManager.RemoveDeviceConnection(c.Identity.Id);
                                    }
                                    else
                                    {
                                        Events.ServiceIdentityUpdatedValidated(serviceIdentity.Id);
                                    }
                                });
                        });
                }
                else
                {
                    Events.DeviceNotConnected(serviceIdentity.Id);
                }
            }
            catch (Exception ex)
            {
                Events.ErrorReauthenticating(serviceIdentity.Id, ex);
            }
        }

        bool IsEdgeHubIdentity(string id) => this.edgeHubIdentity.Id.Equals(id);

        async void ReauthenticateConnections(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                Events.ReauthenticatingClients();
                IList<IIdentity> identities = this.connectionManager.GetConnectedClients().ToList();
                foreach (IIdentity identity in identities)
                {
                    try
                    {
                        if (this.IsEdgeHubIdentity(identity.Id))
                        {
                            continue;
                        }

                        Option<IClientCredentials> clientCredentials = await this.credentialsCache.Get(identity);
                        bool result = await clientCredentials
                            .Map(
                                async c =>
                                {
                                    bool authRes = await this.authenticator.ReauthenticateAsync(c);
                                    Events.ClientCredentialsResult(identity, authRes);
                                    return authRes;
                                })
                            .GetOrElse(
                                () =>
                                {
                                    Events.ClientCredentialsNotFound(identity);
                                    return Task.FromResult(false);
                                });

                        if (!result)
                        {
                            Events.NotReauthenticated(identity.Id);
                            await this.connectionManager.RemoveDeviceConnection(identity.Id);
                        }
                    }
                    catch (Exception e)
                    {
                        Events.ErrorReauthenticating(identity.Id, e);
                    }
                }
            }
            catch (Exception e)
            {
                Events.ErrorReauthenticating(e);
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.PeriodicConnectionAuthenticator;

            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionReauthenticator>();

            enum EventIds
            {
                ErrorReauthenticating = IdStart,

                ErrorRemovingConnection,

                ServiceIdentityUpdated,

                ServiceIdentityUpdatedRemoving,

                ServiceIdentityUpdatedValidated,

                ServiceIdentityRemoved,

                ClientCredentialsResult,

                DeviceNotConnected,

                StartingReauthTimer,

                ReauthenticatingClients,

                EdgeHubConnectionReestablished
            }

            public static void ClientCredentialsNotFound(IIdentity identity)
            {
                Log.LogError((int)EventIds.ClientCredentialsResult, $"Unable to reauthenticate client {identity.Id} as the client credentials were not found, removing client connection");
            }

            public static void ClientCredentialsResult(IIdentity identity, bool result)
            {
                if (result)
                {
                    Log.LogDebug((int)EventIds.ClientCredentialsResult, $"Reauthenticated client {identity.Id} successfully");
                }
                else
                {
                    Log.LogWarning((int)EventIds.ClientCredentialsResult, $"Reauthenticating client {identity.Id} failed, removing client connection");
                }
            }

            public static void DeviceNotConnected(string id)
            {
                Log.LogDebug((int)EventIds.DeviceNotConnected, $"Service identity for {id} in device scope was updated, but {id} is not connected to EdgeHub");
            }

            public static void EdgeHubConnectionReestablished()
            {
                Log.LogDebug((int)EventIds.EdgeHubConnectionReestablished, "EdgeHub cloud connection established, refreshing device scope cache.");
            }

            public static void ErrorReauthenticating(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorReauthenticating, ex, "Error re-authenticating connected clients.");
            }

            public static void ErrorRemovingConnection(Exception exception, string id)
            {
                Log.LogWarning((int)EventIds.ErrorRemovingConnection, exception, $"Error removing connection for {id} after service identity was removed from device scope.");
            }

            public static void NotReauthenticated(string id)
            {
                Log.LogInformation((int)EventIds.ServiceIdentityRemoved, $"Unable to re-authenticate {id}, dropping client connection.");
            }

            public static void ReauthenticatingClients()
            {
                Log.LogInformation((int)EventIds.ReauthenticatingClients, "Reauthenticating connected clients");
            }

            public static void ServiceIdentityRemoved(string id)
            {
                Log.LogInformation((int)EventIds.ServiceIdentityRemoved, $"Service identity for {id} was removed from device scope, dropping client connection.");
            }

            public static void ServiceIdentityUpdated(string serviceIdentityId)
            {
                Log.LogInformation((int)EventIds.ServiceIdentityUpdated, $"Service identity for {serviceIdentityId} in device scope was updated.");
            }

            public static void ServiceIdentityUpdatedRemoving(string serviceIdentityId)
            {
                Log.LogInformation((int)EventIds.ServiceIdentityUpdatedRemoving, $"Service identity for {serviceIdentityId} in device scope was updated, dropping client connection.");
            }

            public static void ServiceIdentityUpdatedValidated(string serviceIdentityId)
            {
                Log.LogDebug((int)EventIds.ServiceIdentityUpdatedValidated, $"Service identity for {serviceIdentityId} in device scope was updated, client connection was re-validated.");
            }

            public static void StartingReauthTimer(Timer timer)
            {
                Log.LogInformation((int)EventIds.StartingReauthTimer, $"Starting timer to authenticate connections with a period of {timer.Interval / 1000} seconds");
            }

            internal static void ErrorReauthenticating(string id, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorReauthenticating, ex, $"Error re-authenticating client {id}, closing the connection.");
            }
        }
    }
}
