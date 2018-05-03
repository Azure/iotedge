// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    public class DeviceIdentityProvider : IDeviceIdentityProvider
    {
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly bool clientCertAuthAllowed;

        public DeviceIdentityProvider(IAuthenticator authenticator, IClientCredentialsFactory clientCredentialsFactory, bool clientCertAuthAllowed)
        {
            this.authenticator = authenticator;
            this.clientCredentialsFactory = clientCredentialsFactory;
            this.clientCertAuthAllowed = clientCertAuthAllowed;
        }

        public async Task<IDeviceIdentity> GetAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(username, nameof(username));
                Preconditions.CheckNonWhiteSpace(clientId, nameof(clientId));

                (string deviceId, string moduleId, string deviceClientType) = ParseUserName(username);
                IClientCredentials deviceCredentials;
                // This is a very weak check for now. In the future, we need to save client certs in a dictionary of
                // module name to client cert. We would then retrieve the cert here. We also will need to handle
                // revocation of certs.
                if (password == null && this.clientCertAuthAllowed)
                {
                    deviceCredentials = this.clientCredentialsFactory.GetWithX509Cert(
                        deviceId,
                        moduleId,
                        deviceClientType);
                }
                else
                {
                    deviceCredentials = this.clientCredentialsFactory.GetWithSasToken(deviceId, moduleId, deviceClientType, password);
                }

                if (deviceCredentials == null
                    || !clientId.Equals(deviceCredentials.Identity.Id, StringComparison.Ordinal)
                    || !await this.authenticator.AuthenticateAsync(deviceCredentials))
                {
                    Events.Error(clientId, username);
                    return UnauthenticatedDeviceIdentity.Instance;
                }
                Events.Success(clientId, username);
                return new ProtocolGatewayIdentity(deviceCredentials);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        internal static (string deviceId, string moduleId, string deviceClientType) ParseUserName(string username)
        {
            // Username is of one of the 2 forms:
            //   username   = edgeHubHostName "/" deviceId [ "/" moduleId ] "?" properties
            //    OR
            //   username   = edgeHubHostName "/" deviceId [ "/" moduleId ] "/" properties
            //   properties = property *("&" property)
            //   property   = name "=" value
            // We recognize two property names:
            //   "api-version" [mandatory]
            //   "DeviceClientType" [optional]
            // We ignore any properties we don't recognize.            
            Preconditions.CheckNonWhiteSpace(username, nameof(username));
            if (username.Contains('?'))
            {
                string[] parts = username.Split('?');
                if (parts.Length > 2)
                {
                    throw new EdgeHubConnectionException($"Username {username} does not contain valid values");
                }

                string[] usernameSegments = parts[0].Split('/');
                if (usernameSegments.Length == 2)
                {
                    return (usernameSegments[1].Trim(), string.Empty, ParseDeviceClientType(parts[1]));
                }
                else if (usernameSegments.Length == 3)
                {
                    return (usernameSegments[1].Trim(), usernameSegments[2].Trim(), ParseDeviceClientType(parts[1]));
                }
                else
                {
                    throw new EdgeHubConnectionException($"Username {username} does not contain valid values");
                }
            }
            else
            {
                string[] usernameSegments = username.Split('/');
                if (usernameSegments.Length == 3 && usernameSegments[2].Contains("api-version="))
                {
                    return (usernameSegments[1].Trim(), string.Empty, ParseDeviceClientType(usernameSegments[2]));
                }
                else if (usernameSegments.Length == 4 && usernameSegments[3].Contains("api-version="))
                {
                    return (usernameSegments[1].Trim(), usernameSegments[2].Trim(), ParseDeviceClientType(usernameSegments[3]));
                }
                // The Azure ML container is using an older client that returns a device client with the following format -
                // username = edgeHubHostName/deviceId/moduleId/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003
                // Notice how the DeviceClientType parameter is separated by a '/' instead of a '&', giving a usernameSegments.Length of 6 instead of the expected 4
                // To allow those clients to work, check for that specific api-version, and version.
                else if (usernameSegments.Length == 6 && username.EndsWith("/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003", StringComparison.OrdinalIgnoreCase))
                {
                    string deviceClientType = "Microsoft.Azure.Devices.Client/1.5.1-preview-003";
                    return (usernameSegments[1].Trim(), usernameSegments[2].Trim(), deviceClientType);
                }
                else
                {
                    throw new EdgeHubConnectionException($"Username {username} does not contain valid values");
                }
            }
        }

        static string ParseDeviceClientType(string queryParams)
        {
            // example input: "api-version=version&DeviceClientType=url-escaped-string&other-prop=value&some-other-prop"

            var kvsep = new[] { '=' };

            Dictionary<string, string> parms = queryParams
                .Split('&')                             // split input string into params
                .Select(s => s.Split(kvsep, 2))         // split each param into a key/value pair
                .GroupBy(s => s[0])                     // group duplicates (by key) together...
                .Select(s => s.First())                 // ...and keep only the first one
                .ToDictionary(                          // convert to Dictionary<string, string>
                    s => s[0],
                    s => s.ElementAtOrEmpty(1));

            return parms.ContainsKey("DeviceClientType") ? Uri.UnescapeDataString(parms["DeviceClientType"]) : string.Empty;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceIdentityProvider>();
            const int IdStart = MqttEventIds.SasTokenDeviceIdentityProvider;

            enum EventIds
            {
                CreateSuccess = IdStart,
                CreateFailure
            }

            public static void Success(string clientId, string username)
            {
                Log.LogInformation((int)EventIds.CreateSuccess, Invariant($"Successfully generated identity for clientId {clientId} and username {username}"));
            }

            public static void Error(string clientId, string username)
            {
                Log.LogError((int)EventIds.CreateFailure, Invariant($"Unable to generate identity for clientId {clientId} and username {username}"));
            }
        }
    }
}
