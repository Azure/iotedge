// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class ServiceIdentityHelpers
    {
        public static ServiceIdentity ToServiceIdentity(this Device device)
        {
            Preconditions.CheckNotNull(device, nameof(device));
            return new ServiceIdentity(
                device.Id,
                null,
                device.Capabilities?.IotEdge ?? false,
                device.Authentication.ToServiceAuthentication());
        }

        public static ServiceIdentity ToServiceIdentity(this Module module)
        {
            Preconditions.CheckNotNull(module, nameof(module));
            return new ServiceIdentity(
                module.Id,
                null,
                false,
                module.Authentication.ToServiceAuthentication());
        }

        public static ServiceAuthentication ToServiceAuthentication(this AuthenticationMechanism authenticationMechanism)
        {
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
