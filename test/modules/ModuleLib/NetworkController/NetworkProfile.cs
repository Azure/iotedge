// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController
{
    using Newtonsoft.Json;

    public class NetworkProfile
    {
        [JsonConverter(typeof(NetworkControllerType))]
        public NetworkControllerType ProfileType { get; set; }

        public NetworkProfileSetting ProfileSetting { get; set; }

        public static NetworkProfile Online { get; } = new NetworkProfile()
        {
            ProfileType = NetworkControllerType.Online,
            ProfileSetting = new NetworkProfileSetting()
        };
    }
}
