// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ClientInfo
    {
        public ClientInfo(string deviceId, string moduleId, string deviceClientType, Option<string> modelId)
        {
            this.DeviceId = deviceId;
            this.ModuleId = moduleId;
            this.DeviceClientType = deviceClientType;
            this.ModelId = modelId;
        }

        public string DeviceId { get; }
        public string ModuleId { get; }
        public string DeviceClientType { get; }
        public Option<string> ModelId { get; }
    }
}
