// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace common
{
    public class DeviceContext
    {
        public string ConnectionString { get; }
        public bool Owned { get; }
        public Device Device { get; }
        public DeviceContext(Device device, bool owned, string hostname)
        {
            this.ConnectionString = 
                $"HostName={hostname};" +
                $"DeviceId={device.Id};" +
                $"SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
            this.Owned = owned;
            this.Device = device;
        }
    }
}