// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AllGoodDeviceProxy : IDeviceProxy
    {
        private string iotHubName = TestContext.IotHubName;
        private string deviceId = TestContext.DeviceId;

        public AllGoodDeviceProxy() => this.Identity = new DeviceIdentity(this.iotHubName, this.deviceId);

        public virtual bool IsActive => true;
        public virtual IIdentity Identity { get; set; }
        public virtual Task CloseAsync(Exception ex) => Task.CompletedTask;
        public virtual Task<Option<IClientCredentials>> GetUpdatedIdentity() => Task.FromResult(Option.Some(new TokenCredentials(this.Identity, "test-token", "test-product-info", true) as IClientCredentials));
        public virtual Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request) => Task.FromResult(new DirectMethodResponse("test-id", new byte[] { 0x01, 0x02 }, 200));
        public virtual Task OnDesiredPropertyUpdates(IMessage desiredProperties) => Task.CompletedTask;
        public virtual Task SendC2DMessageAsync(IMessage message) => Task.CompletedTask;
        public virtual Task SendMessageAsync(IMessage message, string input) => Task.CompletedTask;
        public virtual Task SendTwinUpdate(IMessage twin) => Task.CompletedTask;
        public virtual void SetInactive()
        {
        }

        public AllGoodDeviceProxy WithIdentity(IIdentity identity)
        {
            this.Identity = identity;
            return this;
        }

        public AllGoodDeviceProxy WithHubName(string iotHubName)
        {
            this.iotHubName = iotHubName;
            return this;
        }
    }
}
