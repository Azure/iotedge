// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace common
{
    public class DeviceContext
    {
        public IotHub IotHub { get; }
        public string ConnectionString { get; }
        public bool Owned { get; }
        public Device Device { get; }
        public DeviceContext(Device device, bool owned, IotHub iotHub)
        {
            this.IotHub = iotHub;
            this.ConnectionString = 
                $"HostName={iotHub.Hostname};" +
                $"DeviceId={device.Id};" +
                $"SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
            this.Owned = owned;
            this.Device = device;
        }
    }
}