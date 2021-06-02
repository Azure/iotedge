// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

    public class BrokeredCloudConnectionProvider : ICloudConnectionProvider
    {
        readonly BrokeredCloudProxyDispatcher cloudProxyDispatcher;
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;

        public BrokeredCloudConnectionProvider(BrokeredCloudProxyDispatcher cloudProxyDispatcher, IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache)
        {
            this.cloudProxyDispatcher = cloudProxyDispatcher;
            this.deviceScopeIdentitiesCache = deviceScopeIdentitiesCache;
        }

        public void BindEdgeHub(IEdgeHub edgeHub)
        {
            this.cloudProxyDispatcher.BindEdgeHub(edgeHub);
        }

        public Task<Try<ICloudConnection>> Connect(IClientCredentials clientCredentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            return this.Connect(clientCredentials.Identity, connectionStatusChangedHandler);
        }

        public async Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            if (!await this.IsConnected())
            {
                return Try<ICloudConnection>.Failure(new IotHubException("Bridge is not connected upstream"));
            }

            try
            {
                // TODO: Check disconnect reason and refresh identity when MQTT5 is supported
                // The identity is not refreshed for now because there is no way to detect when the connection was dropped from upstream because unauthorized
                await this.deviceScopeIdentitiesCache.VerifyServiceIdentityAuthChainState(identity.Id, isNestedEdgeEnabled: true, refreshCachedIdentity: false);
            }
            catch (DeviceInvalidStateException ex)
            {
                return Try<ICloudConnection>.Failure(ex);
            }

            var cloudProxy = new BrokeredCloudProxy(identity, this.cloudProxyDispatcher, connectionStatusChangedHandler);
            await cloudProxy.OpenAsync();
            return new Try<ICloudConnection>(new BrokeredCloudConnection(identity, cloudProxy));
        }

        // The purpose of this method is to make less noise in logs when the broker
        // is not connected upstream for a short interim time period.
        // This mainly happens during startup, when edgeHub core starts connecting upstream,
        // but some collaborating components are not ready yet, or the parent device is not
        // available yet.
        async Task<bool> IsConnected()
        {
            var shouldRetry = new FixedInterval(50, TimeSpan.FromMilliseconds(100)).GetShouldRetry();
            var retryCount = 0;

            while (!this.cloudProxyDispatcher.IsConnected && shouldRetry(retryCount++, null, out var delay))
            {
                await Task.Delay(delay);
            }

            return this.cloudProxyDispatcher.IsConnected;
        }
    }
}
