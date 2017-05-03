// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Security.Authentication;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class HubIdentityHelper
    {
        public static Try<HubDeviceIdentity> TryGetHubDeviceIdentityWithSasToken(
            string value, 
            string iotHubHostName, 
            string token)
        {
            return TryGetHubDeviceIdentity(value, iotHubHostName, token, AuthenticationScope.SasToken, null);
        }

        public static Try<HubDeviceIdentity> TryGetHubDeviceIdentityWithHubKey(
            string value,
            string iotHubHostName,
            string keyName,
            string keyValue)
        {
            return TryGetHubDeviceIdentity(value, iotHubHostName, keyValue, AuthenticationScope.HubKey, keyName);
        }

        public static Try<HubDeviceIdentity> TryGetHubDeviceIdentityWithDeviceKey(
            string value,
            string iotHubHostName,
            string keyValue)
        {
            return TryGetHubDeviceIdentity(value, iotHubHostName, keyValue, AuthenticationScope.DeviceKey, null);
        }

        static Try<HubDeviceIdentity> TryGetHubDeviceIdentity(
            string value, 
            string iotHubHostName,
            string secret,
            AuthenticationScope scope,
            string policyName)
        {
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));            

            string[] usernameSegments = Preconditions.CheckNonWhiteSpace(value, nameof(value)).Split('/');
            if (usernameSegments.Length < 2)
            {
                var ex = new InvalidCredentialException("Username does not contain valid values");
                return Try<HubDeviceIdentity>.Failure(ex);
            }

            try
            {
                string deviceHubHostName = usernameSegments[0];
                string deviceId = usernameSegments[1];
                string connectionString = GetConnectionString(iotHubHostName, deviceId, scope, policyName, secret);

                // TODO - Figure out how to set IsAuthenticated
                var hubDeviceIdentity = new HubDeviceIdentity(deviceHubHostName, deviceId, true, connectionString, scope, policyName, secret);
                return hubDeviceIdentity;
            }
            catch (Exception ex)
            {
                return Try<HubDeviceIdentity>.Failure(ex);
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
                    throw new InvalidOperationException($"Unexpected AuthenticationScope value: {scope}");
            }
        }
    }
}