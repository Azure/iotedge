// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
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
            // If 'identity' represents an Edge module then its device id MUST match the authenticator's
            // 'edgeDeviceId'. In other words the authenticator for one edge device cannot authenticate
            // modules belonging to a different edge device.
            var moduleIdentity = identity as IModuleIdentity;
            if (moduleIdentity != null && !moduleIdentity.DeviceId.Equals(this.edgeDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                Events.InvalidDeviceId(moduleIdentity, this.edgeDeviceId);
                return false;
            }

            // Authentication here happens against the cloud, i.e., Azure IoT Hub. We consider a device/module
            // as having authenticated successfully if we are able to acquire a valid ICloudProxy object from
            // the connection manager.

            // Initially we will have many modules connecting with same device ID, so this is a GetOrCreate.
            // When we have module identity implemented, this should be CreateCloudConnectionAsync.
            Try<ICloudProxy> cloudProxyTry = await this.connectionManager.GetOrCreateCloudConnectionAsync(Preconditions.CheckNotNull(identity, nameof(identity)));
            Events.AuthResult(cloudProxyTry, identity.Id);
            return cloudProxyTry.Success && cloudProxyTry.Value.IsActive;
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
                        Log.LogError((int)EventIds.AuthError, Invariant($"Unable to authenticate device {id} because the cloud proxy is not active"));
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
