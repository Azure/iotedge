// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public abstract class MqttConnectionProvider : IMqttConnectionProvider
    {
        readonly IConnectionProvider connectionProvider;

        protected MqttConnectionProvider(IConnectionProvider connectionProvider)
        {
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
        }

        public async Task Connect(string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            IDeviceProxy deviceProxy = this.GetDeviceProxy();
            Try<IDeviceListener> deviceListener = await this.connectionProvider.Connect(connectionString, deviceProxy);
            // Use deviceListener interface to send messages from device to IoTHub
        }

        protected abstract IDeviceProxy GetDeviceProxy();
    }
}
