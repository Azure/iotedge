// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Data;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DeviceScopeTokenAuthenticator : IAuthenticator
    {
        readonly IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache;
        readonly string iothubHostName;
        readonly string edgeHubHostName;
        readonly IAuthenticator underlyingAuthenticator;
        readonly IConnectionManager connectionManager;

        public DeviceScopeTokenAuthenticator(IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            string iothubHostName,
            string edgeHubHostName,
            IAuthenticator underlyingAuthenticator,
            IConnectionManager connectionManager)
        {
            this.underlyingAuthenticator = Preconditions.CheckNotNull(underlyingAuthenticator, nameof(underlyingAuthenticator));
            this.deviceScopeIdentitiesCache = Preconditions.CheckNotNull(deviceScopeIdentitiesCache, nameof(deviceScopeIdentitiesCache));
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.edgeHubHostName = Preconditions.CheckNotNull(edgeHubHostName, nameof(edgeHubHostName));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            //this.deviceScopeIdentitiesCache.ServiceIdentityUpdated += this.HandleServiceIdentityUpdate;
            //this.deviceScopeIdentitiesCache.ServiceIdentityRemoved += this.HandleServiceIdentityRemove;
        }

        //async void HandleServiceIdentityUpdate(object sender, ServiceIdentity serviceIdentity)
        //{
        //    try
        //    {
        //        Events.ServiceIdentityUpdated(serviceIdentity.Id);
        //        Option<IClientCredentials> clientCredentials = this.connectionManager.GetClientCredentials(serviceIdentity.Id);
        //        await clientCredentials.ForEachAsync(
        //            async c =>
        //            {
        //                if (!(c is ITokenCredentials tokenCredentials) ||
        //                    !await this.AuthenticateInternalAsync(tokenCredentials, serviceIdentity))
        //                {
        //                    Events.ServiceIdentityUpdatedRemoving(serviceIdentity.Id);
        //                    await this.connectionManager.RemoveDeviceConnection(c.Identity.Id);
        //                }
        //                else
        //                {
        //                    Events.ServiceIdentityUpdatedValidated(serviceIdentity.Id);
        //                }
        //            });
        //    }
        //    catch (Exception ex)
        //    {
        //        Events.ErrorReauthenticating(ex, serviceIdentity);
        //    }
        //}

        //async void HandleServiceIdentityRemove(object sender, string id)
        //{
        //    try
        //    {
        //        Events.ServiceIdentityRemoved(id);
        //        await this.connectionManager.RemoveDeviceConnection(id);
        //    }
        //    catch (Exception ex)
        //    {
        //        Events.ErrorRemovingConnection(ex, id);
        //    }
        //}

        public async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is ITokenCredentials tokenCredentials))
            {
                return false;
            }

            try
            {
                Option<ServiceIdentity> serviceIdentity = await this.deviceScopeIdentitiesCache.GetServiceIdentity(clientCredentials.Identity.Id);
                return await serviceIdentity.Map(s => this.AuthenticateInternalAsync(tokenCredentials, s))
                    .GetOrElse(() => this.underlyingAuthenticator.AuthenticateAsync(clientCredentials));
            }
            catch (Exception e)
            {
                Events.ErrorAuthenticating(e, clientCredentials);
                return await this.underlyingAuthenticator.AuthenticateAsync(clientCredentials);
            }
        }

        async Task<bool> AuthenticateInternalAsync(ITokenCredentials tokenCredentials, ServiceIdentity serviceIdentity)
        {
            if (!this.TryGetSharedAccessSignature(tokenCredentials.Token, tokenCredentials.Identity, out SharedAccessSignature sharedAccessSignature))
            {
                return false;
            }

            bool result = this.ValidateCredentials(sharedAccessSignature, serviceIdentity, tokenCredentials.Identity);
            if (!result && tokenCredentials.Identity is IModuleIdentity moduleIdentity && serviceIdentity.IsModule)
            {
                // Module can use the Device key to authenticate
                Option<ServiceIdentity> deviceServiceIdentity = await this.deviceScopeIdentitiesCache.GetServiceIdentity(moduleIdentity.DeviceId);
                result = await deviceServiceIdentity.Map(d => this.AuthenticateInternalAsync(tokenCredentials, d))
                    .GetOrElse(Task.FromResult(false));
            }
            return result;
        }

        bool TryGetSharedAccessSignature(string token, IIdentity identity, out SharedAccessSignature sharedAccessSignature)
        {
            try
            {
                sharedAccessSignature = SharedAccessSignature.Parse(this.iothubHostName, token);
                return true;
            }
            catch (Exception e)
            {
                Events.ErrorParsingToken(identity, e);
                sharedAccessSignature = null;
                return false;
            }
        }

        bool ValidateCredentials(SharedAccessSignature sharedAccessSignature, ServiceIdentity serviceIdentity, IIdentity identity) =>
            this.ValidateTokenWithSecurityIdentity(sharedAccessSignature, serviceIdentity) &&
            this.ValidateAudience(sharedAccessSignature.Audience, identity) &&
            this.ValidateExpiry(sharedAccessSignature, identity);

        bool ValidateExpiry(SharedAccessSignature sharedAccessSignature, IIdentity identity)
        {
            if (sharedAccessSignature.IsExpired())
            {
                Events.TokenExpired(identity);
                return false;
            }

            return true;
        }

        bool ValidateTokenWithSecurityIdentity(SharedAccessSignature sharedAccessSignature, ServiceIdentity serviceIdentity)
        {
            if (serviceIdentity.Authentication.Type != ServiceAuthenticationType.SymmetricKey)
            {
                Events.InvalidServiceIdentityType(serviceIdentity);
                return false;
            }

            if (serviceIdentity.Status != ServiceIdentityStatus.Enabled)
            {
                Events.ServiceIdentityNotEnabled(serviceIdentity);
                return false;
            }

            return serviceIdentity.Authentication.SymmetricKey.Map(
                s =>
                {
                    var rule = new SharedAccessSignatureAuthorizationRule
                    {
                        PrimaryKey = s.PrimaryKey,
                        SecondaryKey = s.SecondaryKey
                    };

                    try
                    {
                        sharedAccessSignature.Authenticate(rule);
                        return true;
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Events.KeysMismatch(serviceIdentity.Id, e);
                        return false;
                    }
                })
                .GetOrElse(() => throw new InvalidOperationException($"Unable to validate token because the service identity has empty symmetric keys"));
        }

        internal bool ValidateAudience(string audience, IIdentity identity)
        {
            Preconditions.CheckNonWhiteSpace(audience, nameof(audience));
            audience = WebUtility.UrlDecode(audience.Trim());
            // The audience should be in one of the following formats -
            // {HostName}/devices/{deviceId}/modules/{moduleId}
            // {HostName}/devices/{deviceId}
            string[] parts = audience.Split('/');
            string hostName;
            if (parts.Length == 3)
            {
                hostName = parts[0];
                string deviceId = parts[2];
                if (identity is IDeviceIdentity deviceIdentity && deviceIdentity.DeviceId != deviceId)
                {
                    Events.IdMismatch(audience, identity, deviceIdentity.DeviceId);
                    return false;
                }
                else if(identity is IModuleIdentity moduleIdentity && moduleIdentity.DeviceId != deviceId)
                {
                    Events.IdMismatch(audience, identity, moduleIdentity.DeviceId);
                    return false;
                }
            }
            else if (parts.Length == 5)
            {
                hostName = parts[0];
                string deviceId = parts[2];
                string moduleId = parts[4];
                if (!(identity is IModuleIdentity moduleIdentity))
                {
                    Events.InvalidAudience(audience, identity);
                    return false;
                }
                else if (moduleIdentity.DeviceId != deviceId)
                {
                    Events.IdMismatch(audience, identity, moduleIdentity.DeviceId);
                    return false;
                }
                else if (moduleIdentity.ModuleId != moduleId)
                {
                    Events.IdMismatch(audience, identity, moduleIdentity.ModuleId);
                    return false;
                }
            }
            else
            {
                Events.InvalidAudience(audience, identity);
                return false;
            }

            if (string.IsNullOrWhiteSpace(hostName) ||
                !(this.iothubHostName.Equals(hostName) || this.edgeHubHostName.Equals(hostName)))
            {
                Events.InvalidHostName(identity.Id, hostName, this.iothubHostName, this.edgeHubHostName);
                return false;
            }

            return true;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeTokenAuthenticator>();
            const int IdStart = CloudProxyEventIds.TokenCredentialsAuthenticator;

            enum EventIds
            {
                ErrorReauthenticating = IdStart,
                InvalidHostName,
                InvalidAudience,
                IdMismatch,
                KeysMismatch,
                InvalidServiceIdentityType,
                ErrorAuthenticating,
                ErrorRemovingConnection,
                ServiceIdentityUpdated,
                ServiceIdentityUpdatedRemoving,
                ServiceIdentityUpdatedValidated,
                ServiceIdentityRemoved,
                ServiceIdentityNotEnabled,
                TokenExpired,
                ErrorParsingToken
            }

            public static void ErrorReauthenticating(Exception exception, ServiceIdentity serviceIdentity)
            {
                Log.LogWarning((int)EventIds.ErrorReauthenticating, exception, $"Error re-authenticating {serviceIdentity.Id} after the service identity was updated.");
            }

            public static void InvalidHostName(string id, string hostName, string iotHubHostName, string edgeHubHostName)
            {
                Log.LogWarning((int)EventIds.InvalidHostName, $"Error authenticating token for {id} because the audience hostname {hostName} does not match IoTHub hostname {iotHubHostName} or the EdgeHub hostname {edgeHubHostName}.");
            }

            public static void InvalidAudience(string audience, IIdentity identity)
            {
                Log.LogWarning((int)EventIds.InvalidAudience, $"Error authenticating token for {identity.Id} because the audience {audience} is invalid.");
            }

            public static void IdMismatch(string audience, IIdentity identity, string deviceId)
            {
                Log.LogWarning((int)EventIds.IdMismatch, $"Error authenticating token for {identity.Id} because the deviceId {deviceId} in the identity does not match the audience {audience}.");
            }

            public static void KeysMismatch(string id, Exception exception)
            {
                Log.LogWarning((int)EventIds.KeysMismatch, $"Error authenticating token for {id} because the token did not match the primary or the secondary key. Error - {exception.Message}");
            }

            public static void InvalidServiceIdentityType(ServiceIdentity serviceIdentity)
            {
                Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Error authenticating token for {serviceIdentity.Id} because the service identity authentication type is unexpected - {serviceIdentity.Authentication.Type}");
            }

            public static void ErrorAuthenticating(Exception exception, IClientCredentials credentials)
            {
                Log.LogWarning((int)EventIds.ErrorAuthenticating, exception, $"Error authenticating credentials for {credentials.Identity.Id}");
            }

            public static void ErrorRemovingConnection(Exception exception, string id)
            {
                Log.LogWarning((int)EventIds.ErrorRemovingConnection, exception, $"Error removing connection for {id} after service identity was removed from device scope.");
            }

            public static void ServiceIdentityUpdated(string serviceIdentityId)
            {
                Log.LogDebug((int)EventIds.ServiceIdentityUpdated, $"Service identity for {serviceIdentityId} in device scope was updated.");
            }

            public static void ServiceIdentityUpdatedRemoving(string serviceIdentityId)
            {
                Log.LogDebug((int)EventIds.ServiceIdentityUpdatedRemoving, $"Service identity for {serviceIdentityId} in device scope was updated, dropping client connection.");
            }

            public static void ServiceIdentityUpdatedValidated(string serviceIdentityId)
            {
                Log.LogDebug((int)EventIds.ServiceIdentityUpdatedValidated, $"Service identity for {serviceIdentityId} in device scope was updated, client connection was re-validated.");
            }

            public static void ServiceIdentityRemoved(string id)
            {
                Log.LogDebug((int)EventIds.ServiceIdentityRemoved, $"Service identity for {id} in device scope was removed, dropping client connection.");
            }

            public static void ServiceIdentityNotEnabled(ServiceIdentity serviceIdentity)
            {
                Log.LogWarning((int)EventIds.ServiceIdentityNotEnabled, $"Error authenticating token for {serviceIdentity.Id} because the service identity is not enabled");
            }

            public static void TokenExpired(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.TokenExpired, $"Error authenticating token for {identity.Id} because the token has expired.");
            }

            public static void ErrorParsingToken(IIdentity identity, Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorParsingToken, exception, $"Error authenticating token for {identity.Id} because the token could not be parsed");
            }
        }
    }
}
