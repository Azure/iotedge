// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public static class ServiceIdentityHelpers
    {
        public static ServiceIdentity ToServiceIdentity(this Device device)
        {
            Preconditions.CheckNotNull(device, nameof(device));
            return new ServiceIdentity(
                device.Id,
                null,
                device.GenerationId,
                device.Capabilities.ToServiceCapabilities(),
                device.Authentication.ToServiceAuthentication(),
                device.Status.ToServiceIdentityStatus());
        }

        public static ServiceIdentity ToServiceIdentity(this Module module)
        {
            Preconditions.CheckNotNull(module, nameof(module));
            return new ServiceIdentity(
                module.DeviceId,
                module.Id,
                module.GenerationId,
                Enumerable.Empty<string>(),
                module.Authentication.ToServiceAuthentication(),
                ServiceIdentityStatus.Enabled);
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

        public static ServiceIdentityStatus ToServiceIdentityStatus(this DeviceStatus status)
        {
            switch (status)
            {
                case DeviceStatus.Enabled:
                    return ServiceIdentityStatus.Enabled;
                default:
                    return ServiceIdentityStatus.Disabled;
            }
        }

        public static IEnumerable<string> ToServiceCapabilities(this DeviceCapabilities capabilities)
        {
            Preconditions.CheckNotNull(capabilities, nameof(capabilities));
            var serviceCapabilities = new List<string>();
            if (capabilities.IotEdge)
            {
                serviceCapabilities.Add(Constants.IotEdgeIdentityCapability);
            }

            return serviceCapabilities;
        }
    }
}
