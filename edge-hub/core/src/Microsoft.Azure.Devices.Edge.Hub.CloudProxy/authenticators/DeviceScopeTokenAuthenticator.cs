// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Common.Data;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class DeviceScopeTokenAuthenticator : DeviceScopeAuthenticator<ITokenCredentials>
    {
        readonly string iothubHostName;
        readonly string edgeHubHostName;

        public DeviceScopeTokenAuthenticator(
            IDeviceScopeIdentitiesCache deviceScopeIdentitiesCache,
            string iothubHostName,
            string edgeHubHostName,
            IAuthenticator underlyingAuthenticator,
            bool allowDeviceAuthForModule,
            bool syncServiceIdentityOnFailure,
            bool nestedEdgeEnabled = false)
            : base(deviceScopeIdentitiesCache, underlyingAuthenticator, allowDeviceAuthForModule, syncServiceIdentityOnFailure, nestedEdgeEnabled)
        {
            this.iothubHostName = Preconditions.CheckNonWhiteSpace(iothubHostName, nameof(iothubHostName));
            this.edgeHubHostName = Preconditions.CheckNotNull(edgeHubHostName, nameof(edgeHubHostName));
        }

        protected override (Option<string> deviceId, Option<string> moduleId) GetActorId(ITokenCredentials credentials)
        {
            // Parse the deviceId and moduleId out of the token's audience
            SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(this.iothubHostName, credentials.Token);
            (string hostName, string deviceId, Option<string> moduleId) = this.ParseAudience(sharedAccessSignature.Audience);
            return (Option.Some(deviceId), moduleId);
        }

        protected override bool AreInputCredentialsValid(ITokenCredentials credentials) => this.TryGetSharedAccessSignature(credentials.Token, credentials.Identity, out SharedAccessSignature _);

        protected override bool ValidateWithServiceIdentity(ServiceIdentity serviceIdentity, ITokenCredentials credentials, Option<string> authChain) =>
            this.TryGetSharedAccessSignature(credentials.Token, credentials.Identity, out SharedAccessSignature sharedAccessSignature)
                ? this.ValidateCredentials(sharedAccessSignature, serviceIdentity, credentials.Identity, authChain)
                : false;

        internal (string hostName, string deviceId, Option<string> moduleId) ParseAudience(string audience)
        {
            Preconditions.CheckNonWhiteSpace(audience, nameof(audience));
            audience = WebUtility.UrlDecode(audience.Trim());

            // The audience should be in one of the following formats -
            // {HostName}/devices/{deviceId}/modules/{moduleId}
            // {HostName}/devices/{deviceId}
            string[] parts = audience.Split('/');
            string hostName;
            string deviceId;
            Option<string> moduleId = Option.None<string>();
            if (parts.Length == 3)
            {
                hostName = parts[0];
                deviceId = WebUtility.UrlDecode(parts[2]);
            }
            else if (parts.Length == 5)
            {
                hostName = parts[0];
                deviceId = WebUtility.UrlDecode(parts[2]);
                moduleId = Option.Some(WebUtility.UrlDecode(parts[4]));
            }
            else
            {
                throw new ArgumentException($"Invalid audience: {audience}");
            }

            return (hostName, deviceId, moduleId);
        }

        internal bool ValidateAuthChain(string actorDeviceId, string targetId, string authChain)
        {
            if (AuthChainHelpers.ValidateAuthChain(actorDeviceId, targetId, authChain))
            {
                return true;
            }
            else
            {
                Events.InvalidAuthChain(targetId, authChain);
                return false;
            }
        }

        internal bool ValidateAudienceIds(string audienceDeviceId, Option<string> audienceModuleId, IIdentity identity)
        {
            string audienceId = audienceDeviceId + audienceModuleId.Map(id => "/" + id).GetOrElse(string.Empty);

            if (!audienceModuleId.HasValue)
            {
                // Token is for a device
                if (identity is IDeviceIdentity deviceIdentity && !string.Equals(deviceIdentity.DeviceId, audienceDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    Events.IdMismatch(audienceId, identity, deviceIdentity.DeviceId);
                    return false;
                }
                else if (identity is IModuleIdentity moduleIdentity && moduleIdentity.DeviceId != audienceDeviceId)
                {
                    Events.IdMismatch(audienceId, identity, moduleIdentity.DeviceId);
                    return false;
                }
            }
            else
            {
                // Token is for a module
                string moduleId = audienceModuleId.Expect(() => new InvalidOperationException());

                if (!(identity is IModuleIdentity moduleIdentity))
                {
                    Events.InvalidAudience(audienceId, identity);
                    return false;
                }
                else if (moduleIdentity.DeviceId != audienceDeviceId)
                {
                    Events.IdMismatch(audienceId, identity, moduleIdentity.DeviceId);
                    return false;
                }
                else if (moduleIdentity.ModuleId != moduleId)
                {
                    Events.IdMismatch(audienceId, identity, moduleIdentity.ModuleId);
                    return false;
                }
            }

            return true;
        }

        internal bool ValidateAudience(string audience, IIdentity identity, Option<string> authChain)
        {
            string hostName;
            string deviceId;
            Option<string> moduleId;

            try
            {
                (hostName, deviceId, moduleId) = this.ParseAudience(audience);
            }
            catch
            {
                Events.InvalidAudience(audience, identity);
                return false;
            }

            if (authChain.HasValue)
            {
                // OnBehalfOf scenario, where we need to check the audience against the authchain
                if (!this.ValidateAuthChain(deviceId, identity.Id, authChain.Expect(() => new InvalidOperationException())))
                {
                    return false;
                }
            }
            else
            {
                // Default scenario, we just need to the check the audience against the target identity
                if (!this.ValidateAudienceIds(deviceId, moduleId, identity))
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(hostName) ||
                !(this.iothubHostName.Equals(hostName) || this.edgeHubHostName.Equals(hostName)))
            {
                Events.InvalidHostName(identity.Id, hostName, this.iothubHostName, this.edgeHubHostName);
                return false;
            }

            return true;
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

        bool ValidateCredentials(SharedAccessSignature sharedAccessSignature, ServiceIdentity serviceIdentity, IIdentity identity, Option<string> authChain) =>
            this.ValidateTokenWithSecurityIdentity(sharedAccessSignature, serviceIdentity) &&
            this.ValidateAudience(sharedAccessSignature.Audience, identity, authChain) &&
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

        static class Events
        {
            const int IdStart = CloudProxyEventIds.TokenCredentialsAuthenticator;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeTokenAuthenticator>();

            enum EventIds
            {
                InvalidHostName = IdStart,
                InvalidAudience,
                InvalidAuthChain,
                IdMismatch,
                KeysMismatch,
                InvalidServiceIdentityType,
                ServiceIdentityNotEnabled,
                TokenExpired,
                ErrorParsingToken
            }

            public static void InvalidHostName(string id, string hostName, string iotHubHostName, string edgeHubHostName)
            {
                Log.LogWarning((int)EventIds.InvalidHostName, $"Error authenticating token for {id} because the audience hostname {hostName} does not match IoTHub hostname {iotHubHostName} or the EdgeHub hostname {edgeHubHostName}.");
            }

            public static void InvalidAudience(string audience, IIdentity identity)
            {
                Log.LogWarning((int)EventIds.InvalidAudience, $"Error authenticating token for {identity.Id} because the audience {audience} is invalid.");
            }

            public static void InvalidAuthChain(string id, string authChain)
            {
                Log.LogWarning((int)EventIds.InvalidAuthChain, $"Error authenticating token for {id} because the auth-chain {authChain} is invalid.");
            }

            public static void IdMismatch(string audienceId, IIdentity identity, string deviceId)
            {
                Log.LogWarning((int)EventIds.IdMismatch, $"Error authenticating token for {identity.Id} because the deviceId {deviceId} in the identity does not match the audienceId {audienceId}.");
            }

            public static void KeysMismatch(string id, Exception exception)
            {
                Log.LogWarning((int)EventIds.KeysMismatch, $"Error authenticating token for {id} because the token did not match the primary or the secondary key. Error - {exception.Message}");
            }

            public static void InvalidServiceIdentityType(ServiceIdentity serviceIdentity)
            {
                Log.LogWarning((int)EventIds.InvalidServiceIdentityType, $"Error authenticating token for {serviceIdentity.Id} because the service identity authentication type is unexpected - {serviceIdentity.Authentication.Type}");
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
                Log.LogInformation((int)EventIds.ErrorParsingToken, $"Error authenticating token for {identity.Id} because the token is expired or could not be parsed");
                Log.LogDebug((int)EventIds.ErrorParsingToken, exception, $"Error parsing token for {identity.Id}");
            }
        }
    }
}
