// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Data;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DeviceScopeCertificateAuthenticator : DeviceScopeAuthenticator<ICertificateCredentials>
    {
        readonly string iothubHostName;
        readonly IList<X509Certificate2> trustBundle;

        public DeviceScopeCertificateAuthenticator(
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            string iothubHostName,
            IAuthenticator underlyingAuthenticator,
            IList<X509Certificate2> trustBundle,
            bool syncServiceIdentityOnFailure)
            :
            base(deviceScopeIdentitiesCache, underlyingAuthenticator, false, syncServiceIdentityOnFailure)
        {
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
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
                result = CertificateHelper.ValidateClientCert(certificateCredentials.ClientCertificate,
                                                              certificateCredentials.ClientCertificateChain,
                                                              Option.None<IList<X509Certificate2>>(),
                                                              Events.Log);
                if (!result)
                {
                    Events.InvalidCertificate(serviceIdentity.Id, certificateCredentials);
                }
                else
                {
                    result = serviceIdentity.Authentication.X509Thumbprint.Map(
                        t =>
                        {
                            List<string> thumbprints = new List<string>() { t.PrimaryThumbprint, t.SecondaryThumbprint };
                            return CertificateHelper.ValidateCertificateThumbprint(certificateCredentials.ClientCertificate, thumbprints);
                        })
                        .GetOrElse(() => throw new InvalidOperationException($"Unable to validate certificate because the service identity has empty thumbprints"));
                    if (!result)
                    {
                        Events.ThumbprintMismatch(serviceIdentity.Id);
                    }
                }
            }
            else if (serviceIdentity.Authentication.Type == ServiceAuthenticationType.CertificateAuthority)
            {
                if (!CertificateHelper.ValidateCommonName(certificateCredentials.ClientCertificate, serviceIdentity.DeviceId))
                {
                    Events.InvalidCommonName(serviceIdentity.Id);
                    result = false;
                }
                else if (!CertificateHelper.ValidateClientCert(certificateCredentials.ClientCertificate,
                                                               certificateCredentials.ClientCertificateChain,
                                                               Option.Some(this.trustBundle),
                                                               Events.Log))
                {
                    Events.InvalidCertificate(serviceIdentity.Id, certificateCredentials);
                    result = false;
                }
                else
                {
                    result = true;
                }
            }
            else
            {
                Events.InvalidServiceIdentityType(serviceIdentity);
                result = false;
            }

            return result;
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
                ErrorAuthenticating,
                ServiceIdentityNotEnabled,
                ServiceIdentityNotFound,
                AuthenticatedInScope,
                InvalidCertificateURI
            }

            public static void UnsupportedClientIdentityType(string id) => Log.LogWarning((int)EventIds.UnsupportedClientIdentityType, $"Error authenticating {id} using X.509 certificates since this is identity type is unsupported.");

            public static void ThumbprintMismatch(string id) => Log.LogWarning((int)EventIds.ThumbprintMismatch, $"Error authenticating certificate for {id} because the certificate thumbprint did not match the primary or the secondary thumbprints.");

            public static void InvalidCommonName(string id) => Log.LogWarning((int)EventIds.InvalidCommonName, $"Error authenticating certificate for id {id} because the certificate common name (CN) did not match the id");

            public static void InvalidCertificate(string id, ICertificateCredentials certificateCredentials) => Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Invalid certificate with subject {certificateCredentials.ClientCertificate.Subject} for {id}");

            public static void InvalidCertificateUri(string id, ICertificateCredentials certificateCredentials) => Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Certificate for id {id} with subject {certificateCredentials.ClientCertificate.Subject} does not contain the module URI");

            public static void InvalidServiceIdentityType(ServiceIdentity serviceIdentity) => Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Error authenticating token for {serviceIdentity.Id} because the service identity authentication type is unexpected - {serviceIdentity.Authentication.Type}");

            public static void ErrorAuthenticating(Exception exception, IClientCredentials credentials) => Log.LogWarning((int)EventIds.ErrorAuthenticating, exception, $"Error authenticating credentials for {credentials.Identity.Id}");

            public static void ServiceIdentityNotEnabled(ServiceIdentity serviceIdentity) => Log.LogWarning((int)EventIds.ServiceIdentityNotEnabled, $"Error authenticating token for {serviceIdentity.Id} because the service identity is not enabled");

            public static void ServiceIdentityNotFound(IIdentity identity) => Log.LogDebug((int)EventIds.ServiceIdentityNotFound, $"Service identity for {identity.Id} not found. Using underlying authenticator to authenticate");

            public static void AuthenticatedInScope(IIdentity identity, bool isAuthenticated)
            {
                string authenticated = isAuthenticated ? "authenticated" : "not authenticated";
                Log.LogInformation((int)EventIds.AuthenticatedInScope, $"Client {identity.Id} in device scope {authenticated} locally.");
            }

            public static void ReauthenticatedInScope(IIdentity identity, bool isAuthenticated)
            {
                string authenticated = isAuthenticated ? "reauthenticated" : "not reauthenticated";
                Log.LogDebug((int)EventIds.AuthenticatedInScope, $"Client {identity.Id} in device scope {authenticated} locally.");
            }
        }
    }
}
