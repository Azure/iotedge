// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace common
{
    public class DeviceContext
    {
        public CloudContext CloudContext { get; }
        public string ConnectionString { get; }
        public bool Owned { get; }
        public Device Device { get; }
        public DeviceContext(Device device, bool owned, CloudContext cloud)
        {
            this.CloudContext = cloud;
            this.ConnectionString = 
                $"HostName={cloud.Hostname};" +
                $"DeviceId={device.Id};" +
                $"SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
            this.Owned = owned;
            this.Device = device;
        }
    }
}