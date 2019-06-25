// Copyright (c) Microsoft. All rights reserved.

namespace IotEdgeQuickstart.Details
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class DPSAttestation
    {
        public string ScopeId { get; }
        public Option<string> RegistrationId { get; }
        public Option<string> SymmetricKey { get; }
        public Option<string> DeviceIdentityCertificate { get; }
        public Option<string> DeviceIdentityPrivateKey { get; }

        public DPSAttestation(string scopeId, string registrationId, string symmetricKey)
        {
            this.ScopeId = Preconditions.CheckNotNull(scopeId, nameof(scopeId));
            this.RegistrationId = Option.Some(Preconditions.CheckNotNull(registrationId, nameof(registrationId)));
            this.SymmetricKey = Option.Some(Preconditions.CheckNotNull(symmetricKey, nameof(symmetricKey)));
            this.DeviceIdentityCertificate = Option.None<string>();
            this.DeviceIdentityPrivateKey = Option.None<string>();
        }

        public DPSAttestation(string scopeId, Option<string> registrationId, string certPath, string privateKeyPath)
        {
            this.ScopeId = Preconditions.CheckNotNull(scopeId, nameof(scopeId));
            this.RegistrationId = registrationId;
            this.SymmetricKey = Option.None<string>();
            this.DeviceIdentityCertificate = Option.Some(Preconditions.CheckNotNull(certPath, nameof(certPath)));
            this.DeviceIdentityPrivateKey = Option.Some(Preconditions.CheckNotNull(privateKeyPath, nameof(privateKeyPath)));
        }
    }
}
