// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using Newtonsoft.Json;

    public class NetworkProfileSetting
    {
        public uint Delay { get; set; }

        public uint Jitter { get; set; }

        public uint Bandwidth { get; set; }

        [JsonConverter(typeof(BandwidthUnitType))]
        public BandwidthUnitType BandwidthUnit { get; set; }

        public uint PackageLoss { get; set; }

        public override string ToString()
        {
            return $"Delay: {this.Delay} Jitter: {this.Jitter} Bandwidth: {this.Bandwidth}{this.BandwidthUnit} PackageLoss: {this.PackageLoss}";
        }

        public enum BandwidthUnitType
        {
            bit,
            kbit,
            mbit,
            gbit,
            tbit,
            bps,
            kbps,
            mbps,
            gbps,
            tbps
        }
    }
}
