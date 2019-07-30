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
            var certUri = new System.Uri(Preconditions.CheckNonWhiteSpace(certPath, nameof(certPath)));
            this.DeviceIdentityCertificate = Option.Some(certUri.AbsoluteUri);
            var keyUri = new System.Uri(Preconditions.CheckNonWhiteSpace(privateKeyPath, nameof(privateKeyPath)));
            this.DeviceIdentityPrivateKey = Option.Some(keyUri.AbsoluteUri);
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
