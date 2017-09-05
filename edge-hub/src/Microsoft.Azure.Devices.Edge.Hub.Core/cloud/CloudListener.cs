// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
	using System.Threading.Tasks;
	using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
	using Microsoft.Azure.Devices.Edge.Util;

	class CloudListener : ICloudListener
	{
		readonly IDeviceProxy deviceProxy;
		readonly IEdgeHub edgeHub;
		readonly IIdentity identity;

		public CloudListener(IDeviceProxy deviceProxy, IEdgeHub edgeHub, IIdentity identity)
		{
			this.deviceProxy = Preconditions.CheckNotNull(deviceProxy, nameof(deviceProxy));
			this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
			this.identity = Preconditions.CheckNotNull(identity, nameof(identity));
		}

		public Task<DirectMethodResponse> CallMethodAsync(DirectMethodRequest request) => this.deviceProxy.InvokeMethodAsync(request);

		public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.edgeHub.UpdateDesiredPropertiesAsync(identity.Id, desiredProperties);

		public Task ProcessMessageAsync(IMessage message) => this.deviceProxy.SendC2DMessageAsync(message);
	}
}
