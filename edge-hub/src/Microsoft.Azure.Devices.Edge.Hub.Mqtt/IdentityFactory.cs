// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Security.Authentication;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class IdentityFactory : IIdentityFactory
    {
        readonly string iotHubHostName;

        public IdentityFactory(string iotHubHostName)
        {
            this.iotHubHostName = iotHubHostName;
        }

        public Try<Identity> GetWithSasToken(string username, string password) => this.GetIdentity(username, password, AuthenticationScope.SasToken, null);

        public Try<Identity> GetWithHubKey(string username, string keyName, string keyValue) => this.GetIdentity(username, keyValue, AuthenticationScope.HubKey, keyName);

        public Try<Identity> GetWithDeviceKey(string username, string keyValue) => this.GetIdentity(username, keyValue, AuthenticationScope.DeviceKey, null);

        Try<Identity> GetIdentity(string username, string secret, AuthenticationScope scope, string policyName)
        {
            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));            

            string[] usernameSegments = Preconditions.CheckNonWhiteSpace(username, nameof(username)).Split('/');
            if (usernameSegments.Length < 2)
            {
                var ex = new InvalidCredentialException("Username does not contain valid values");
                return Try<Identity>.Failure(ex);
            }

            try
            {
                string deviceHubHostName = usernameSegments[0];
                string deviceId = usernameSegments[1];

                // Currently, we build the device connection string for both devices and modules
                // Once modules have their identity in IoTHub, this will have to construct a different connection string
                // depending on whether it is a module or a device.
                string connectionString = GetConnectionString(this.iotHubHostName, deviceId, scope, policyName, secret);

                // The username is of the following format - 
                // For Device identity - iothubHostName/deviceId/api-version=version/DeviceClientType=clientType
                // For Module identity - iothubHostName/deviceId/moduleId/api-version=version/DeviceClientType=clientType
                // So we use the hack below to identify if it is a module identity or a device identity
                if (usernameSegments.Length >= 3 && usernameSegments[2].IndexOf("=", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    string moduleId = usernameSegments[2];
                    // IsAuthenticated is always true, except for a special UnauthenticatedIdentity.
                    var hubDeviceIdentity = new ModuleIdentity(deviceHubHostName, deviceId, moduleId, true, connectionString, scope, policyName, secret);
                    return hubDeviceIdentity;
                }
                else
                {
                    // IsAuthenticated is always true, except for a special UnauthenticatedIdentity.
                    var hubDeviceIdentity = new DeviceIdentity(deviceHubHostName, deviceId, true, connectionString, scope, policyName, secret);
                    return hubDeviceIdentity;
                }
            }
            catch (Exception ex)
            {
                return Try<Identity>.Failure(ex);
            }
        }

        internal static string GetConnectionString(string iotHubHostName, string deviceId, AuthenticationScope scope, string policyName, string secret)
        {            
            IAuthenticationMethod authenticationMethod = DeriveAuthenticationMethod(deviceId, scope, policyName, secret);
            IotHubConnectionStringBuilder csb = IotHubConnectionStringBuilder.Create(iotHubHostName, authenticationMethod);
            string connectionString = csb.ToString();
            return connectionString;
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