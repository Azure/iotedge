// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// The <c>IdentityFactory</c> is responsible for creating <see cref="Identity"/> instances
    /// given device/module credentials. Implementations of this interface are expected to
    /// derive the right kind of identity instance (<see cref="DeviceIdentity"/> or <see cref="ModuleIdentity"/>)
    /// by examining the credentials.
    /// </summary>
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

        public Try<IIdentity> GetWithX509Cert(
            string deviceId,
            string moduleId,
            string deviceClientType,
            bool isModuleIdentity
            ) => this.GetIdentity(deviceId, moduleId, deviceClientType, isModuleIdentity, null, AuthenticationScope.x509Cert, null);

        public Try<IIdentity> GetWithSasToken(
            string deviceId,
            string moduleId,
            string deviceClientType,
            bool isModuleIdentity,
            string token) =>
            this.GetIdentity(deviceId, moduleId, deviceClientType, isModuleIdentity, token, AuthenticationScope.SasToken, null);

        public Try<IIdentity> GetWithHubKey(
            string deviceId,
            string moduleId,
            string deviceClientType,
            bool isModuleIdentity,
            string keyName,
            string keyValue) => this.GetIdentity(deviceId, moduleId, deviceClientType, isModuleIdentity, keyValue, AuthenticationScope.HubKey, keyName);

        public Try<IIdentity> GetWithDeviceKey(
            string deviceId,
            string moduleId,
            string deviceClientType,
            bool isModuleIdentity,
            string keyValue) => this.GetIdentity(deviceId, moduleId, deviceClientType, isModuleIdentity, keyValue, AuthenticationScope.DeviceKey, null);

        Try<IIdentity> GetIdentity(string deviceId, string moduleId, string deviceClientType, bool isModuleIdentity, string secret, AuthenticationScope scope, string policyName)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            
            deviceClientType = deviceClientType ?? string.Empty;

            try
            {
                if (isModuleIdentity)
                {
                    Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
                    string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
                    switch (scope)
                    {
                        case AuthenticationScope.x509Cert:
                            return new ModuleIdentity(this.iotHubHostName, deviceId, moduleId, scope, productInfo);
                        default:
                            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));
                            string connectionString = GetConnectionString(this.iotHubHostName, deviceId, moduleId, secret);
                            return new ModuleIdentity(this.iotHubHostName, deviceId, moduleId, connectionString, scope, policyName, secret, productInfo, Option.Some(secret));
                    }
                }
                else
                {
                    string productInfo = string.Join(" ", this.callerProductInfo, deviceClientType).Trim();
                    switch (scope)
                    {
                        case AuthenticationScope.x509Cert:
                            return new DeviceIdentity(this.iotHubHostName, deviceId, scope, productInfo);
                        default:
                            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));
                            string connectionString = GetConnectionString(this.iotHubHostName, deviceId, scope, policyName, secret);
                            return new DeviceIdentity(this.iotHubHostName, deviceId, connectionString, scope, policyName, secret, productInfo, Option.Some(secret));
                    }
                }
            }
            catch (Exception ex)
            {
                return Try<IIdentity>.Failure(ex);
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
                    ? new DeviceIdentity(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, connectionString, parsedResult.scope, parsedResult.policyName, parsedResult.secret, this.callerProductInfo, Option.None<string>()) as IIdentity
                    : new ModuleIdentity(iotHubConnectionStringBuilder.HostName, iotHubConnectionStringBuilder.DeviceId, iotHubConnectionStringBuilder.ModuleId, connectionString, parsedResult.scope, parsedResult.policyName, parsedResult.secret, this.callerProductInfo, Option.None<string>());
                return Try.Success(identity);
            }
            catch (Exception ex)
            {
                return Try<IIdentity>.Failure(ex);
            }
        }

        static (AuthenticationScope scope, string policyName, string secret) GetConnectionStringAuthDetails(IotHubConnectionStringBuilder iotHubConnectionStringBuilder)
        {
            switch (iotHubConnectionStringBuilder.AuthenticationMethod)
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
    }
}
