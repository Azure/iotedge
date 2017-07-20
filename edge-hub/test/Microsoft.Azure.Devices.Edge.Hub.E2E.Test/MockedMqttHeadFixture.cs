// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Moq;

    public class MockedMqttHeadFixture : ProtocolHeadFixture
    {
        public IConnectionManager ConnectionManager { get; }
        public Mock<IDeviceListener> DeviceListener { get; }

        public MockedMqttHeadFixture()
        {
            (this.ConnectionManager, this.DeviceListener) = this.StartMqttHeadWithMocks().Result;
        }
    }
}
