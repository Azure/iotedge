// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util;

    public class CloudListener : ICloudListener
    {
        readonly IEdgeHub edgeHub;
        readonly string clientId;

        public CloudListener(IEdgeHub edgeHub, string clientId)
        {
            this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.clientId = Preconditions.CheckNotNull(clientId, nameof(clientId));
        }

        public Task<DirectMethodResponse> CallMethodAsync(DirectMethodRequest request) => this.edgeHub.InvokeMethodAsync(this.clientId, request);

        public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.edgeHub.UpdateDesiredPropertiesAsync(this.clientId, desiredProperties);

        public Task ProcessMessageAsync(IMessage message) => this.edgeHub.SendC2DMessageAsync(this.clientId, message);
    }
}
