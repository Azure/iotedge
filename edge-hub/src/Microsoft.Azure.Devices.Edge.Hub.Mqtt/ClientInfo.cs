// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    public class ClientInfo
    {
        public ClientInfo(string deviceId, string moduleId, string deviceClientType)
        {
            this.DeviceId = deviceId;
            this.ModuleId = moduleId;
            this.DeviceClientType = deviceClientType;
        }

        public string DeviceId { get; }
        public string ModuleId { get; }
        public string DeviceClientType { get; }
    }
}
