namespace IotEdgeQuickstart.Details
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceProvisioningMethod
    {
        Option<string> ManualConnectionString { get; }
        Option<DPSAttestation> Dps { get; }

        public DeviceProvisioningMethod(string deviceConnectionString)
        {
            this.ManualConnectionString = Option.Some(Preconditions.CheckNonWhiteSpace(deviceConnectionString, nameof(deviceConnectionString)));
            this.Dps = Option.None<DPSAttestation>();
        }

        public DeviceProvisioningMethod(Option<DPSAttestation> dps)
        {
            this.ManualConnectionString = Option.None<string>();
            this.Dps = dps;
        }
    }
}
