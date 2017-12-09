// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class IdentityFactory : IIdentityFactory
    {
        readonly string iotHubHostName;
        readonly string callerProductInfo;

        public IdentityFactory(string iotHubHostName)
            : this(iotHubHostName, string.Empty)
        {
        }

        public IdentityFactory(string iotHubHostName, string callerProductInfo)
        {
            this.iotHubHostName = iotHubHostName;
            this.callerProductInfo = callerProductInfo;
        }

        public Try<IIdentity> GetWithSasToken(string username, string password) => this.GetIdentity(username, password, AuthenticationScope.SasToken, null);

        public Try<IIdentity> GetWithHubKey(string username, string keyName, string keyValue) => this.GetIdentity(username, keyValue, AuthenticationScope.HubKey, keyName);

        public Try<IIdentity> GetWithDeviceKey(string username, string keyValue) => this.GetIdentity(username, keyValue, AuthenticationScope.DeviceKey, null);

        Try<IIdentity> GetIdentity(string username, string secret, AuthenticationScope scope, string policyName)
        {
            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));
            try
            {
                (string iothubHostName, string deviceId, string moduleId, string deviceClientType, bool isModuleIdentity) = ParseUserName(username);
                if (isModuleIdentity)
                {
                    string connectionString = GetConnectionString(this.iotHubHostName, deviceId, moduleId, secret);
                    string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
                    return new ModuleIdentity(iothubHostName, deviceId, moduleId, connectionString, scope, policyName, secret, productInfo);
                }
                else
                {
                    string connectionString = GetConnectionString(this.iotHubHostName, deviceId, scope, policyName, secret);
                    string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
                    return new DeviceIdentity(iothubHostName, deviceId, connectionString, scope, policyName, secret, productInfo);
                }
            }
            catch (Exception ex)
            {
                return Try<IIdentity>.Failure(ex);
            }
        }

        internal static (string iothubHostName, string deviceId, string moduleId, string deviceClientType, bool isModuleIdentity) ParseUserName(string username)
        {
            // Username is of the form:
            //   username   = iothubHostname "/" deviceId "/" [moduleId "/"] properties
            //   properties = property *("&" property)
            //   property   = name "=" value
            // We recognize two property names:
            //   "api-version" [mandatory]
            //   "DeviceClientType" [optional]
            // We ignore any properties we don't recognize.            

            string[] usernameSegments = Preconditions.CheckNonWhiteSpace(username, nameof(username)).Split('/');
            if (usernameSegments.Length == 3 && usernameSegments[2].Contains("api-version="))
            {
                return (usernameSegments[0], usernameSegments[1], string.Empty, ParseDeviceClientType(usernameSegments[2]), false);
            }
            else if (usernameSegments.Length == 4 && usernameSegments[3].Contains("api-version="))
            {
                return (usernameSegments[0], usernameSegments[1], usernameSegments[2], ParseDeviceClientType(usernameSegments[3]), true);
            }
            // The Azure ML container is using an older client that returns a device client with the following format -
            // username = iothubHostName/deviceId/moduleId/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003
            // Notice how the DeviceClientType parameter is separated by a '/' instead of a '&', giving a usernameSegments.Length of 6 instead of the expected 4
            // To allow those clients to work, check for that specific api-version, and version.
            else if (usernameSegments.Length == 6 && username.EndsWith("/api-version=2017-06-30/DeviceClientType=Microsoft.Azure.Devices.Client/1.5.1-preview-003", StringComparison.OrdinalIgnoreCase))
            {
                string deviceClientType = "Microsoft.Azure.Devices.Client/1.5.1-preview-003";
                return (usernameSegments[0], usernameSegments[1], usernameSegments[2], deviceClientType, true);
            }
            else
            {
                throw new EdgeHubConnectionException("Username does not contain valid values");
            }
        }

        public Try<IIdentity> GetWithConnectionString(string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            try
            {
                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
                (AuthenticationScope scope, string policyName, string secret) parsedResult = GetConnectionStringAuthDetails(iotHubConnectionStringBuilder);
                IIdentity identity = string.IsNullOrWhiteSpace(iotHubConnectionStringBuilder.ModuleId)
                    ? new DeviceIdentity(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, connectionString, parsedResult.scope, parsedResult.policyName, parsedResult.secret, this.callerProductInfo)
                    : new ModuleIdentity(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, iotHubConnectionStringBuilder.ModuleId, connectionString, parsedResult.scope, parsedResult.policyName, parsedResult.secret, this.callerProductInfo) as IIdentity;
                return Try.Success(identity);
            }
            catch (Exception ex)
            {
                return Try<IIdentity>.Failure(ex);
            }
        }

        static (AuthenticationScope scope, string policyName, string secret) GetConnectionStringAuthDetails(IotHubConnectionStringBuilder iotHubConnectionStringBuilder)
        {
            switch(iotHubConnectionStringBuilder.AuthenticationMethod)
            {
                case DeviceAuthenticationWithToken auth:
                    return (AuthenticationScope.SasToken, null, auth.Token);
                case DeviceAuthenticationWithRegistrySymmetricKey auth:
                    return (AuthenticationScope.DeviceKey, null, auth.KeyAsBase64String);
                case ModuleAuthenticationWithToken auth:
                    return (AuthenticationScope.SasToken, null, auth.Token);
                case ModuleAuthenticationWithRegistrySymmetricKey auth:
                    return (AuthenticationScope.DeviceKey, null, auth.KeyAsBase64String);
                default:
                    throw new InvalidOperationException($"Unexpected authentication method type - {iotHubConnectionStringBuilder.AuthenticationMethod.GetType()}");
            }
        }

        internal static string GetConnectionString(string iotHubHostName, string deviceId, AuthenticationScope scope, string policyName, string secret)
        {
            IAuthenticationMethod authenticationMethod = DeriveAuthenticationMethod(deviceId, scope, policyName, secret);
            IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(iotHubHostName, authenticationMethod);
            string connectionString = csb.ToString();
            return connectionString;
        }

        internal static string GetConnectionString(string iotHubHostName, string deviceId, string moduleId, string secret)
        {
            // TODO - Temporary workaround since DeviceAuthenticationWithToken does not support module identity
            return $"HostName={iotHubHostName};DeviceId={deviceId};ModuleId={moduleId};SharedAccessSignature={secret}";
        }

        static IAuthenticationMethod DeriveAuthenticationMethod(
            string id,
            AuthenticationScope scope,
            string policyName,
            string secret)
        {
            switch (scope)
            {
                case AuthenticationScope.SasToken:
                    return new DeviceAuthenticationWithToken(id, secret);
                case AuthenticationScope.DeviceKey:
                    return new DeviceAuthenticationWithRegistrySymmetricKey(id, secret);
                case AuthenticationScope.HubKey:
                    return new DeviceAuthenticationWithSharedAccessPolicyKey(id, policyName, secret);
                default:
                    throw new InvalidOperationException($"Unexpected AuthenticationScope username: {scope}");
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
    }
}
