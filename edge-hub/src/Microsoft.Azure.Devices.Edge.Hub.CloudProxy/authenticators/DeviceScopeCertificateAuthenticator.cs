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

    public class DeviceScopeCertificateAuthenticator : IAuthenticator
    {
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly string iothubHostName;
        readonly IAuthenticator underlyingAuthenticator;
        readonly IList<X509Certificate2> trustBundle;

        public DeviceScopeCertificateAuthenticator(
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            string iothubHostName,
            IAuthenticator underlyingAuthenticator,
            IList<X509Certificate2> trustBundle)
        {
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
            this.underlyingAuthenticator = Preconditions.CheckNotNull(underlyingAuthenticator, nameof(underlyingAuthenticator));
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.trustBundle = Preconditions.CheckNotNull(trustBundle, nameof(trustBundle));
        }

        public Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, false);

        public Task<bool> ReauthenticateAsync(IClientCredentials clientCredentials)
            => this.AuthenticateAsync(clientCredentials, true);

        async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials, bool reAuthenticating)
        {
            if (!(clientCredentials is ICertificateCredentials certificateCredentials))
            {
                return false;
            }

            Option<ServiceIdentity> serviceIdentity = await this.deviceScopeIdentitiesCache.GetServiceIdentity(clientCredentials.Identity.Id, reAuthenticating);
            if (serviceIdentity.HasValue)
            {
                // currently authenticating modules via X.509 is disabled. all the necessary pieces to authenticate
                // modules via X.509 CA are implemented below and to enable modules to authenticate remove this check
                if (certificateCredentials.Identity is IModuleIdentity)
                {
                    Events.UnsupportedClientIdentityType(certificateCredentials.Identity.Id);
                    return false;
                }
                else
                {
                    try
                    {
                        bool isAuthenticated = await serviceIdentity.Map(s => this.AuthenticateInternalAsync(certificateCredentials, s)).GetOrElse(Task.FromResult(false));
                        if (reAuthenticating)
                        {
                            Events.ReauthenticatedInScope(clientCredentials.Identity, isAuthenticated);
                        }
                        else
                        {
                            Events.AuthenticatedInScope(clientCredentials.Identity, isAuthenticated);
                        }
                        return isAuthenticated;
                    }
                    catch (Exception e)
                    {
                        Events.ErrorAuthenticating(e, clientCredentials);
                        return await this.underlyingAuthenticator.ReauthenticateAsync(clientCredentials);
                    }
                }
            }
            else
            {
                Events.ServiceIdentityNotFound(clientCredentials.Identity);
                return await this.underlyingAuthenticator.ReauthenticateAsync(clientCredentials);
            }
        }

        async Task<bool> AuthenticateInternalAsync(ICertificateCredentials certificateCredentials, ServiceIdentity serviceIdentity) =>
            await Task.FromResult(this.ValidateCertificateWithSecurityIdentity(certificateCredentials, serviceIdentity));

        bool ValidateCertificateWithSecurityIdentity(ICertificateCredentials certificateCredentials, ServiceIdentity serviceIdentity)
        {
            bool result;

            if (serviceIdentity.Status != ServiceIdentityStatus.Enabled)
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
                if (certificateCredentials.Identity is IModuleIdentity)
                {
                    result = serviceIdentity.ModuleId.Map(
                        moduleId =>
                        {
                            return CertificateHelper.ValidateIotHubSanUri(certificateCredentials.ClientCertificate,
                                                                          iothubHostName,
                                                                          serviceIdentity.DeviceId,
                                                                          moduleId);
                        })
                        .GetOrElse(() => throw new InvalidOperationException($"Unable to validate certificate because the service identity is not a module"));
                    if (!result)
                    {
                        Events.InvalidCertificateUri(serviceIdentity.Id, certificateCredentials);
                    }
                }
                else
                {
                    result = CertificateHelper.ValidateCommonName(certificateCredentials.ClientCertificate, serviceIdentity.DeviceId);
                    if (!result)
                    {
                        Events.InvalidCommonName(serviceIdentity.Id);
                    }
                }

                if (result && (!CertificateHelper.ValidateClientCert(certificateCredentials.ClientCertificate,
                                                                     certificateCredentials.ClientCertificateChain,
                                                                     Option.Some(this.trustBundle),
                                                                     Events.Log)))
                {
                    Events.InvalidCertificate(serviceIdentity.Id, certificateCredentials);
                    result = false;
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
