// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    public class NetworkProfileSetting
    {
        public int Delay { get; set; }

        public int Jitter { get; set; }

        public string Bandwidth { get; set; }

        public int PackageLoss { get; set; }
    }
}
