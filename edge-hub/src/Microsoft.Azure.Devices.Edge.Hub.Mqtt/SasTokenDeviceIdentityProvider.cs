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

    public class SasTokenDeviceIdentityProvider : IDeviceIdentityProvider
    {
        readonly IAuthenticator authenticator;
        readonly IIdentityFactory identityFactory;

        public SasTokenDeviceIdentityProvider(IAuthenticator authenticator, IIdentityFactory identityFactory)
        {
            this.authenticator = authenticator;
            this.identityFactory = identityFactory;
        }

        public async Task<IDeviceIdentity> GetAsync(string clientId, string username, string password, EndPoint clientAddress)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(username, nameof(username));
                Preconditions.CheckNonWhiteSpace(password, nameof(password));
                Preconditions.CheckNonWhiteSpace(clientId, nameof(clientId));

                (string deviceId, string moduleId, string deviceClientType, bool isModuleIdentity) = ParseUserName(username);

                Try<IIdentity> deviceIdentity = this.identityFactory.GetWithSasToken(deviceId, moduleId, deviceClientType, isModuleIdentity, password);
                if (!deviceIdentity.Success
                    || !clientId.Equals(deviceIdentity.Value.Id, StringComparison.Ordinal)
                    || !await this.authenticator.AuthenticateAsync(deviceIdentity.Value))
                {
                    Events.Error(clientId, username);
                    return UnauthenticatedDeviceIdentity.Instance;
                }
                Events.Success(clientId, username);
                return new ProtocolGatewayIdentity(deviceIdentity.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        internal static (string deviceId, string moduleId, string deviceClientType, bool isModuleIdentity) ParseUserName(string username)
        {
            // Username is of the form:
            //   username   = edgeHubHostName "/" deviceId "/" [moduleId "/"] properties
            //   properties = property *("&" property)
            //   property   = name "=" value
            // We recognize two property names:
            //   "api-version" [mandatory]
            //   "DeviceClientType" [optional]
            // We ignore any properties we don't recognize.            

            string[] usernameSegments = Preconditions.CheckNonWhiteSpace(username, nameof(username)).Split('/');
            if (usernameSegments.Length == 3 && usernameSegments[2].Contains("api-version="))
            {
                return (usernameSegments[1], string.Empty, ParseDeviceClientType(usernameSegments[2]), false);
            }
            else if (usernameSegments.Length == 4 && usernameSegments[3].Contains("api-version="))
            {
                return (usernameSegments[1], usernameSegments[2], ParseDeviceClientType(usernameSegments[3]), true);
            }
            // The Azure ML container is using an older client that returns a device client with the following format -
            // username = edgeHubHostName/deviceId/moduleId/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003
            // Notice how the DeviceClientType parameter is separated by a '/' instead of a '&', giving a usernameSegments.Length of 6 instead of the expected 4
            // To allow those clients to work, check for that specific api-version, and version.
            else if (usernameSegments.Length == 6 && username.EndsWith("/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003", StringComparison.OrdinalIgnoreCase))
            {
                string deviceClientType = "Microsoft.Azure.Devices.Client/1.5.1-preview-003";
                return (usernameSegments[1], usernameSegments[2], deviceClientType, true);
            }
            else
            {
                throw new EdgeHubConnectionException("Username does not contain valid values");
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
            static readonly ILogger Log = Logger.Factory.CreateLogger<SasTokenDeviceIdentityProvider>();
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
