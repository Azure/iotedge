// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class CloudTokenAuthenticator : IAuthenticator
    {
        readonly IConnectionManager connectionManager;

        public CloudTokenAuthenticator(IConnectionManager connectionManager)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
        }

        public async Task<bool> AuthenticateAsync(IClientCredentials clientCredentials)
        {
            if (!(clientCredentials is ITokenCredentials))
            {
                return false;
            }

                Try<ICloudProxy> cloudProxyTry = await this.connectionManager.CreateCloudConnectionAsync(clientCredentials);
                if (cloudProxyTry.Success)
                {
                    try
                    {
                        await cloudProxyTry.Value.OpenAsync();
                        Events.AuthenticatedWithIotHub(clientCredentials.Identity);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Events.ErrorValidatingTokenWithIoTHub(clientCredentials.Identity, ex);
                    }
            }
            else
            {
                Events.ErrorGettingCloudProxy(clientCredentials.Identity, cloudProxyTry.Exception);
            }

            return false;
        }


        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudTokenAuthenticator>();
            const int IdStart = HubCoreEventIds.CloudTokenAuthenticator;

            enum EventIds
            {
                AuthenticatedWithCloud = IdStart,
                ErrorValidatingToken,
                ErrorGettingCloudProxy
            }

            public static void AuthenticatedWithIotHub(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.AuthenticatedWithCloud, $"Authenticated {identity.Id} with IotHub");
            }            

            public static void ErrorValidatingTokenWithIoTHub(IIdentity identity, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorValidatingToken, ex, $"Error validating token for {identity.Id} with IoTHub");
            }

            public static void ErrorGettingCloudProxy(IIdentity identity, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGettingCloudProxy, ex, $"Error getting cloud proxy for {identity.Id}");
            }
        }
    }
}
