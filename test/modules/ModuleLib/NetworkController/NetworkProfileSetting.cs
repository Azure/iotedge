// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController
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
            Bit,
            Kbit,
            Mbit,
            Gbit,
            Tbit,
            Bps,
            Kbps,
            Mbps,
            Gbps,
            Tbps
        }
    }
}
