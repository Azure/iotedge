// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using Microsoft.Azure.Devices.Edge.Util;

    public enum DPSAttestationType
    {
        SymmetricKey,
        Tpm,
        X509
    }

    public class DPSAttestation
    {
        public DPSAttestation(string endPoint, string scopeId, string registrationId, string symmetricKey)
        {
            this.EndPoint = Preconditions.CheckNonWhiteSpace(endPoint, nameof(endPoint));
            this.ScopeId = Preconditions.CheckNonWhiteSpace(scopeId, nameof(scopeId));
            this.RegistrationId = Option.Some(Preconditions.CheckNonWhiteSpace(registrationId, nameof(registrationId)));
            this.SymmetricKey = Option.Some(Preconditions.CheckNonWhiteSpace(symmetricKey, nameof(symmetricKey)));
            this.DeviceIdentityCertificate = Option.None<string>();
            this.DeviceIdentityPrivateKey = Option.None<string>();
            this.AttestationType = DPSAttestationType.SymmetricKey;
        }

        public DPSAttestation(string endPoint, string scopeId, Option<string> registrationId, string certPath, string privateKeyPath)
        {
            this.EndPoint = Preconditions.CheckNonWhiteSpace(endPoint, nameof(endPoint));
            this.ScopeId = Preconditions.CheckNonWhiteSpace(scopeId, nameof(scopeId));
            this.RegistrationId = registrationId;
            this.SymmetricKey = Option.None<string>();
            this.DeviceIdentityCertificate = Option.Some(Preconditions.CheckNonWhiteSpace(certPath, nameof(certPath)));
            this.DeviceIdentityPrivateKey = Option.Some(Preconditions.CheckNonWhiteSpace(privateKeyPath, nameof(privateKeyPath)));
            this.AttestationType = DPSAttestationType.X509;
        }

        public string EndPoint { get; }

        public string ScopeId { get; }

        public DPSAttestationType AttestationType { get; }

        public Option<string> RegistrationId { get; }

        public Option<string> SymmetricKey { get; }

        public Option<string> DeviceIdentityCertificate { get; }

        public Option<string> DeviceIdentityPrivateKey { get; }
    }
}
