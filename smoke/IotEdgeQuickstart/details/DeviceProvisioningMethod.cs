// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceProvisioningMethod
    {
        public DeviceProvisioningMethod(string deviceConnectionString)
        {
            this.ManualConnectionString = Option.Some(Preconditions.CheckNonWhiteSpace(deviceConnectionString, nameof(deviceConnectionString)));
            this.Dps = Option.None<DPSAttestation>();
        }

        public DeviceProvisioningMethod(DPSAttestation dps)
        {
            this.ManualConnectionString = Option.None<string>();
            this.Dps = Option.Some(Preconditions.CheckNotNull(dps, nameof(dps)));
        }

        public Option<string> ManualConnectionString { get; }

        public Option<DPSAttestation> Dps { get; }
    }
}
