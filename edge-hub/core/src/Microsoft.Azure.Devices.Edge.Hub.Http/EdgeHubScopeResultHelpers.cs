// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Org.BouncyCastle.Security;

    public static class EdgeHubScopeResultHelpers
    {
        public static EdgeHubScopeDevice ToEdgeHubScopeDevice(this ServiceIdentity identity)
        {
            Preconditions.CheckNotNull(identity);
            Preconditions.CheckArgument(!identity.IsModule);

            return new EdgeHubScopeDevice(
                identity.DeviceId,
                identity.GenerationId,
                GetDeviceStatus(identity.Status),
                GetAuthenticationMechanism(identity.Authentication),
                GetDeviceCapabilities(identity.IsEdgeDevice),
                identity.DeviceScope.OrDefault(),
                identity.ParentScopes);
        }

        public static EdgeHubScopeModule ToEdgeHubScopeModule(this ServiceIdentity identity)
        {
            Preconditions.CheckNotNull(identity);
            Preconditions.CheckArgument(identity.IsModule);
            string moduleId = identity.ModuleId.Expect(() => new InvalidParameterException($"ModuleId shouldn't be empty when ServiceIdentity is a module: {identity.Id}"));

            return new EdgeHubScopeModule(
                Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId)),
                identity.DeviceId,
                identity.GenerationId,
                GetAuthenticationMechanism(identity.Authentication));
        }

        static DeviceStatus GetDeviceStatus(ServiceIdentityStatus status)
            => (status == ServiceIdentityStatus.Enabled) ? DeviceStatus.Enabled : DeviceStatus.Disabled;

        static DeviceCapabilities GetDeviceCapabilities(bool isEdgeDevice)
            => new DeviceCapabilities() { IotEdge = isEdgeDevice };

        static AuthenticationMechanism GetAuthenticationMechanism(ServiceAuthentication serviceAuth)
        {
            var authentication = new AuthenticationMechanism();

            switch (serviceAuth.Type)
            {
                case ServiceAuthenticationType.SymmetricKey:
                    authentication.Type = AuthenticationType.Sas;
                    var sasKey = serviceAuth.SymmetricKey.Expect(() => new InvalidParameterException("SAS key shouldn't be empty when auth type is SymmetricKey"));
                    authentication.SymmetricKey = new SymmetricKey() { PrimaryKey = sasKey.PrimaryKey, SecondaryKey = sasKey.SecondaryKey };
                    break;

                case ServiceAuthenticationType.CertificateThumbprint:
                    authentication.Type = AuthenticationType.SelfSigned;
                    var x509Thumbprint = serviceAuth.X509Thumbprint.Expect(() => new InvalidParameterException("X509 thumbprint shouldn't be empty when auth type is CertificateThumbPrint"));
                    authentication.X509Thumbprint = new X509Thumbprint() { PrimaryThumbprint = x509Thumbprint.PrimaryThumbprint, SecondaryThumbprint = x509Thumbprint.SecondaryThumbprint };
                    break;

                case ServiceAuthenticationType.CertificateAuthority:
                    authentication.Type = AuthenticationType.CertificateAuthority;
                    break;

                case ServiceAuthenticationType.None:
                    authentication.Type = AuthenticationType.None;
                    break;

                default:
                    throw new InvalidParameterException($"Unexpected ServiceAuthenticationType: {serviceAuth.Type}");
            }

            return authentication;
        }

        public static ServiceAuthentication ToServiceAuthentication(this AuthenticationMechanism authenticationMechanism)
        {
            Preconditions.CheckNotNull(authenticationMechanism, nameof(authenticationMechanism));
            switch (authenticationMechanism.Type)
            {
                case AuthenticationType.CertificateAuthority:
                    return new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority);

                case AuthenticationType.SelfSigned:
                    return new ServiceAuthentication(
                        new X509ThumbprintAuthentication(authenticationMechanism.X509Thumbprint.PrimaryThumbprint, authenticationMechanism.X509Thumbprint.SecondaryThumbprint));

                case AuthenticationType.Sas:
                    return new ServiceAuthentication(
                        new SymmetricKeyAuthentication(authenticationMechanism.SymmetricKey.PrimaryKey, authenticationMechanism.SymmetricKey.SecondaryKey));

                default:
                    return new ServiceAuthentication(ServiceAuthenticationType.None);
            }
        }
    }
}
