// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

    public class DeviceScopeApiClientProvider : IDeviceScopeApiClientProvider
    {
        readonly string iotHubHostName;
        readonly string actorEdgeDeviceId;
        readonly string moduleId;
        readonly int batchSize;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly IAuthenticationChainProvider authChainProvider;
        readonly Option<IWebProxy> proxy;
        readonly RetryStrategy retryStrategy;

        public DeviceScopeApiClientProvider(
            string iotHubHostName,
            string deviceId,
            string moduleId,
            int batchSize,
            ITokenProvider edgeHubTokenProvider,
            IAuthenticationChainProvider authChainProvider,
            Option<IWebProxy> proxy,
            RetryStrategy retryStrategy = null)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.actorEdgeDeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.batchSize = Preconditions.CheckRange(batchSize, 0, 1000, nameof(batchSize));
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.authChainProvider = Preconditions.CheckNotNull(authChainProvider, nameof(authChainProvider));
            this.proxy = Preconditions.CheckNotNull(proxy, nameof(proxy));
            this.retryStrategy = retryStrategy;
        }

        public IDeviceScopeApiClient CreateDeviceScopeClient()
        {
            return new DeviceScopeApiClient(
                this.iotHubHostName,
                this.actorEdgeDeviceId,
                this.moduleId,
                this.batchSize,
                this.edgeHubTokenProvider,
                this.proxy,
                this.retryStrategy);
        }

        public IDeviceScopeApiClient CreateNestedDeviceScopeClient(Option<string> continuationToken)
        {
            return new NestedDeviceScopeApiClient(
                this.iotHubHostName,
                this.actorEdgeDeviceId,
                this.moduleId,
                continuationToken,
                this.batchSize,
                this.edgeHubTokenProvider,
                this.authChainProvider,
                this.proxy,
                this.retryStrategy);
        }

        public IDeviceScopeApiClient CreateOnBehalfOf(string childDeviceId, Option<string> continuationToken)
        {
            return new NestedDeviceScopeApiClient(
                this.iotHubHostName,
                this.actorEdgeDeviceId,
                childDeviceId,
                this.moduleId,
                continuationToken,
                this.batchSize,
                this.edgeHubTokenProvider,
                this.authChainProvider,
                this.proxy,
                this.retryStrategy);
        }
    }
}
