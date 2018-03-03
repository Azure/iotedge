// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.CertificateHelper;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class Authenticator : IAuthenticator
    {
        readonly IConnectionManager connectionManager;
        readonly string edgeDeviceId;

        public Authenticator(IConnectionManager connectionManager, string edgeDeviceId)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(edgeDeviceId));
            this.edgeDeviceId = Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
        }

        public async Task<bool> AuthenticateAsync(IIdentity identity)
        {
            Preconditions.CheckNotNull(identity);

            // If 'identity' represents an Edge module then its device id MUST match the authenticator's
            // 'edgeDeviceId'. In other words the authenticator for one edge device cannot authenticate
            // modules belonging to a different edge device.
            if (identity is IModuleIdentity moduleIdentity && !moduleIdentity.DeviceId.Equals(this.edgeDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                Events.InvalidDeviceId(moduleIdentity, this.edgeDeviceId);
                return false;
            }

            if (identity.Scope == AuthenticationScope.x509Cert)
            {
                // If we reach here, we have validated the client cert. Validation is done in
                // DeviceIdentityProvider.cs::RemoteCertificateValidationCallback. In the future, we could
                // do authentication based on the CN. However, EdgeHub does not have enough information
                // currently to do CN validation.
                return true;
            }
            else
            {
                // Authentication here happens against the cloud (Azure IoT Hub). We consider a device/module
                // as having authenticated successfully if we are able to acquire a valid ICloudProxy object from
                // the connection manager.

                Try<ICloudProxy> cloudProxyTry = await this.connectionManager.CreateCloudConnectionAsync(identity);
                Events.AuthResult(cloudProxyTry, identity.Id);
                return cloudProxyTry.Success && cloudProxyTry.Value.IsActive;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<Authenticator>();
            const int IdStart = HubCoreEventIds.Authenticator;

            enum EventIds
            {
                AuthSuccess = IdStart,
                AuthError,
                InvalidDeviceError
            }

            public static void InvalidDeviceId(IModuleIdentity moduleIdentity, string edgeDeviceId)
            {
                Log.LogError((int)EventIds.InvalidDeviceError, Invariant($"Device Id {moduleIdentity.DeviceId} of module {moduleIdentity.ModuleId} is different from the edge device Id {edgeDeviceId}"));
            }

            public static void AuthResult(Try<ICloudProxy> cloudProxyTry, string id)
            {
                if (cloudProxyTry.Success)
                {
                    if (cloudProxyTry.Value.IsActive)
                    {
                        Log.LogInformation((int)EventIds.AuthSuccess, Invariant($"Successfully authenticated device {id}"));
                    }
                    else
                    {
                        Log.LogInformation((int)EventIds.AuthError, Invariant($"Unable to authenticate device {id}"));
                    }
                }
                else
                {
                    Log.LogError((int)EventIds.AuthError, cloudProxyTry.Exception, Invariant($"Unable to authenticate device {id}"));
                }
            }
        }
    }
}
