// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DeviceScopeCertificateAuthenticator : DeviceScopeAuthenticator<ICertificateCredentials>
    {
        readonly IList<X509Certificate2> trustBundle;

        public DeviceScopeCertificateAuthenticator(
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            IAuthenticator underlyingAuthenticator,
            IList<X509Certificate2> trustBundle,
            bool syncServiceIdentityOnFailure,
            bool nestedEdgeEnabled = true)
            : base(deviceScopeIdentitiesCache, underlyingAuthenticator, false, syncServiceIdentityOnFailure, nestedEdgeEnabled)
        {
            this.trustBundle = Preconditions.CheckNotNull(trustBundle, nameof(trustBundle));
        }

        // we return true here since a client certificates and its chain will be validated ValidateWithServiceIdentity
        // a possibility would be to check if things are null but that is already being done in CertificateCredentials
        protected override bool AreInputCredentialsValid(ICertificateCredentials credentials) => true;

        protected override bool ValidateWithServiceIdentity(ServiceIdentity serviceIdentity, ICertificateCredentials certificateCredentials)
        {
            bool result;
            // currently authenticating modules via X.509 is disabled. all the necessary pieces to authenticate
            // modules via X.509 CA are implemented below and to enable modules to authenticate remove this check
            if (certificateCredentials.Identity is IModuleIdentity)
            {
                Events.UnsupportedClientIdentityType(certificateCredentials.Identity.Id);
                result = false;
            }
            else if (serviceIdentity.Status != ServiceIdentityStatus.Enabled)
            {
                Events.ServiceIdentityNotEnabled(serviceIdentity);
                result = false;
            }
            else if (serviceIdentity.Authentication.Type == ServiceAuthenticationType.CertificateThumbprint)
            {
                result = this.ValidateThumbprintAuth(serviceIdentity, certificateCredentials);
            }
            else if (serviceIdentity.Authentication.Type == ServiceAuthenticationType.CertificateAuthority)
            {
                result = this.ValidateCaAuth(serviceIdentity, certificateCredentials);
            }
            else
            {
                Events.InvalidServiceIdentityType(serviceIdentity);
                result = false;
            }

            return result;
        }

        bool ValidateCaAuth(ServiceIdentity serviceIdentity, ICertificateCredentials certificateCredentials)
        {
            if (!CertificateHelper.ValidateCommonName(certificateCredentials.ClientCertificate, serviceIdentity.DeviceId))
            {
                Events.InvalidCommonName(serviceIdentity.Id);
                return false;
            }

            if (!CertificateHelper.ValidateClientCert(
                certificateCredentials.ClientCertificate,
                certificateCredentials.ClientCertificateChain,
                Option.Some(this.trustBundle),
                Events.Log))
            {
                Events.InvalidCertificate(serviceIdentity.Id, certificateCredentials);
                return false;
            }

            return true;
        }

        bool ValidateThumbprintAuth(ServiceIdentity serviceIdentity, ICertificateCredentials certificateCredentials)
        {
            if (serviceIdentity.Authentication.Type == ServiceAuthenticationType.CertificateThumbprint
                && !serviceIdentity.Authentication.X509Thumbprint.HasValue)
            {
                Events.ThumbprintServiceIdentityInvalid(serviceIdentity);
                return false;
            }

            X509ThumbprintAuthentication x509ThumbprintAuthentication =
                serviceIdentity.Authentication.X509Thumbprint.Expect(() => new InvalidOperationException("Service identity with CertificateThumbprint should have X509ThumbprintAuthentication value"));

            if (!CertificateHelper.ValidateCertExpiry(certificateCredentials.ClientCertificate, Events.Log))
            {
                Events.InvalidCertificate(serviceIdentity.Id, certificateCredentials);
                return false;
            }

            string[] thumbprints = { x509ThumbprintAuthentication.PrimaryThumbprint, x509ThumbprintAuthentication.SecondaryThumbprint };
            if (!CertificateHelper.ValidateCertificateThumbprint(certificateCredentials.ClientCertificate, thumbprints))
            {
                Events.ThumbprintMismatch(serviceIdentity.Id);
                return false;
            }

            return true;
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeCertificateAuthenticator>();
            const int IdStart = CloudProxyEventIds.CertificateCredentialsAuthenticator;

            enum EventIds
            {
                UnsupportedClientIdentityType = IdStart,
                ThumbprintMismatch,
                InvalidCommonName,
                InvalidServiceIdentityType,
                ServiceIdentityNotEnabled,
                ServiceIdentityNotFound
            }

            public static void UnsupportedClientIdentityType(string id) => Log.LogWarning((int)EventIds.UnsupportedClientIdentityType, $"Error authenticating {id} using X.509 certificates since this is identity type is unsupported.");

            public static void ThumbprintMismatch(string id) => Log.LogWarning((int)EventIds.ThumbprintMismatch, $"Error authenticating certificate for {id} because the certificate thumbprint did not match the primary or the secondary thumbprints.");

            public static void InvalidCommonName(string id) => Log.LogWarning((int)EventIds.InvalidCommonName, $"Error authenticating certificate for id {id} because the certificate common name (CN) did not match the id");

            public static void InvalidCertificate(string id, ICertificateCredentials certificateCredentials) => Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Invalid certificate with subject {certificateCredentials.ClientCertificate.Subject} for {id}");

            public static void InvalidServiceIdentityType(ServiceIdentity serviceIdentity)
                => Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Error authenticating certificate credentials for client {serviceIdentity.Id} because the service identity authentication type is {serviceIdentity.Authentication.Type}");

            public static void ServiceIdentityNotEnabled(ServiceIdentity serviceIdentity) => Log.LogWarning((int)EventIds.ServiceIdentityNotEnabled, $"Error authenticating token for {serviceIdentity.Id} because the service identity is not enabled");

            public static void ThumbprintServiceIdentityInvalid(ServiceIdentity serviceIdentity)
                => Log.LogDebug((int)EventIds.ServiceIdentityNotFound, $"Service identity for {serviceIdentity.Id} has type {serviceIdentity.Authentication.Type} but X509ThumbprintAuthentication value is null");
        }
    }
}
