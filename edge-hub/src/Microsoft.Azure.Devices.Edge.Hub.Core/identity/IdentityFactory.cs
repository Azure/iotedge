// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class IdentityFactory : IIdentityFactory
    {
        readonly string iotHubHostName;

        public IdentityFactory(string iotHubHostName)
        {
            this.iotHubHostName = iotHubHostName;
        }

        public Try<IIdentity> GetWithSasToken(string username, string password) => this.GetIdentity(username, password, AuthenticationScope.SasToken, null);

        public Try<IIdentity> GetWithHubKey(string username, string keyName, string keyValue) => this.GetIdentity(username, keyValue, AuthenticationScope.HubKey, keyName);

        public Try<IIdentity> GetWithDeviceKey(string username, string keyValue) => this.GetIdentity(username, keyValue, AuthenticationScope.DeviceKey, null);

        Try<IIdentity> GetIdentity(string username, string secret, AuthenticationScope scope, string policyName)
        {
            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));

            string[] usernameSegments = Preconditions.CheckNonWhiteSpace(username, nameof(username)).Split('/');
            if (usernameSegments.Length < 2)
            {
                var ex = new EdgeHubConnectionException("Username does not contain valid values");
                return Try<IIdentity>.Failure(ex);
            }

            try
            {
                string deviceHubHostName = usernameSegments[0];
                string deviceId = usernameSegments[1];

                // The username is of the following format -
                // For Device identity - iothubHostName/deviceId/api-version=version/DeviceClientType=clientType
                // For Module identity - iothubHostName/deviceId/moduleId/api-version=version/DeviceClientType=clientType
                // So we use the hack below to identify if it is a module identity or a device identity
                if (usernameSegments.Length >= 3 && usernameSegments[2].IndexOf("=", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    string moduleId = usernameSegments[2];
                    string connectionString = GetConnectionString(this.iotHubHostName, deviceId, moduleId, secret);
                    // IsAuthenticated is always true, except for a special UnauthenticatedIdentity.
                    var hubDeviceIdentity = new ModuleIdentity(deviceHubHostName, deviceId, moduleId, connectionString, scope, policyName, secret);
                    return hubDeviceIdentity;
                }
                else
                {
                    string connectionString = GetConnectionString(this.iotHubHostName, deviceId, scope, policyName, secret);
                    // IsAuthenticated is always true, except for a special UnauthenticatedIdentity.
                    var hubDeviceIdentity = new DeviceIdentity(deviceHubHostName, deviceId, connectionString, scope, policyName, secret);
                    return hubDeviceIdentity;
                }
            }
            catch (Exception ex)
            {
                return Try<IIdentity>.Failure(ex);
            }
        }

        public Try<IIdentity> GetWithSasToken(string connectionString)
        {
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));
            try
            {
                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(connectionString);
                IIdentity identity = string.IsNullOrWhiteSpace(iotHubConnectionStringBuilder.ModuleId)
                    ? new DeviceIdentity(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, connectionString, AuthenticationScope.SasToken, null, iotHubConnectionStringBuilder.SharedAccessSignature) as IIdentity
                    : new ModuleIdentity(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, iotHubConnectionStringBuilder.ModuleId, connectionString, AuthenticationScope.SasToken, null, iotHubConnectionStringBuilder.SharedAccessSignature);
                return Try.Success(identity);
            }
            catch (Exception ex)
            {
                return Try<IIdentity>.Failure(ex);
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
    }
}