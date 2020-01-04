// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;

    public class NetworkProfile
    {
        public string ProfileType { get; set; }

        public ProfileSetting ProfileSettings { get; set; }

        public class ProfileSetting
        {
            public int Delay { get; set; }

            public int Jitter { get; set; }

            public string Bandwidth { get; set; }

            public int PackageLoss { get; set; }
        }
    }
}
