// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper
{
    using System;
    using System.Collections;
    using Microsoft.Azure.Devices.Client;

    public class DeviceClientFactory
    {
        const string EdgeletUriVariableName = "IotEdge_EdgeletUri";
        const string EdgeletApiVersionVariableName = "IotEdge_EdgeletVersion";
        const string HostnameVariableName = "IotEdge_IotHubHostname";
        const string GatewayVariableName = "IotEdge_Gateway";
        const string DeviceIdVariableName = "IotEdge_DeviceId";
        const string ModuleIdVariableName = "IotEdge_ModuleId";
        const string AuthSchemeVariableName = "IotEdge_AuthScheme";
        const string SasTokenAuthScheme = "SasToken";
        const string EdgehubConnectionstringVariableName = "EdgeHubConnectionString";
        const string IothubConnectionstringVariableName = "IotHubConnectionString";

        readonly TransportType? transportType;
        readonly ITransportSettings[] transportSettings;

        public DeviceClientFactory()
        {
        }

        public DeviceClientFactory(TransportType transporType)
        {
            this.transportType = transporType;
        }

        public DeviceClientFactory(ITransportSettings[] transportSettings)
        {
            this.transportSettings = transportSettings;
        }

        public DeviceClient Create()
        {
            return this.CreateDeviceClientFromEnvironment();
        }

        DeviceClient CreateDeviceClientFromEnvironment()
        {
            IDictionary envVariables = Environment.GetEnvironmentVariables();

            string connectionString = this.GetValueFromEnvironment(envVariables, EdgehubConnectionstringVariableName) ?? this.GetValueFromEnvironment(envVariables, IothubConnectionstringVariableName);

            // First try to create from connection string and if env variable for connection string is not found try to create from edgeletUri
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return this.CreateDeviceClientFromConnectionString(connectionString);
            }
            else
            {
                string edgeletUri = this.GetValueFromEnvironment(envVariables, EdgeletUriVariableName) ?? throw new InvalidOperationException($"Environement variable {EdgeletUriVariableName} is required.");
                string deviceId = this.GetValueFromEnvironment(envVariables, DeviceIdVariableName) ?? throw new InvalidOperationException($"Environement variable {DeviceIdVariableName} is required.");
                string moduleId = this.GetValueFromEnvironment(envVariables, ModuleIdVariableName) ?? throw new InvalidOperationException($"Environement variable {ModuleIdVariableName} is required.");
                string hostname = this.GetValueFromEnvironment(envVariables, HostnameVariableName) ?? throw new InvalidOperationException($"Environement variable {HostnameVariableName} is required.");
                string authScheme = this.GetValueFromEnvironment(envVariables, AuthSchemeVariableName) ?? throw new InvalidOperationException($"Environement variable {AuthSchemeVariableName} is required.");
                string gateway = this.GetValueFromEnvironment(envVariables, GatewayVariableName);
                string apiVersion = this.GetValueFromEnvironment(envVariables, EdgeletApiVersionVariableName);

                if (!string.Equals(authScheme, SasTokenAuthScheme, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Unsupported authentication scheme. Supported scheme is {SasTokenAuthScheme}.");
                }

                ISignatureProvider signatureProvider = string.IsNullOrWhiteSpace(apiVersion)
                    ? new EdgeletSignatureProvider(edgeletUri)
                    : new EdgeletSignatureProvider(edgeletUri, apiVersion);
                var authMethod = new ModuleAuthenticationWithEdgeToken(signatureProvider, deviceId, moduleId);

                return this.CreateDeviceClientFromAuthenticationMethod(hostname, gateway, authMethod);
            }
        }

        DeviceClient CreateDeviceClientFromConnectionString(string connectionString)
        {
            if (this.transportSettings != null)
            {
                return DeviceClient.CreateFromConnectionString(connectionString, this.transportSettings);
            }

            if (this.transportType.HasValue)
            {
                return DeviceClient.CreateFromConnectionString(connectionString, this.transportType.Value);
            }

            return DeviceClient.CreateFromConnectionString(connectionString);
        }

        DeviceClient CreateDeviceClientFromAuthenticationMethod(string hostname, string gateway, IAuthenticationMethod authMethod)
        {
            //TODO: modify DeviceSdk to set gateway when using create with IAuthMethod
            if (this.transportSettings != null)
            {
                return DeviceClient.Create(hostname, authMethod, this.transportSettings);
            }

            if (this.transportType.HasValue)
            {
                return DeviceClient.Create(hostname, authMethod, this.transportType.Value);
            }

            return DeviceClient.Create(hostname, authMethod);
        }

        string GetValueFromEnvironment(IDictionary envVariables, string variableName)
        {
            if (envVariables.Contains(variableName))
            {
                return envVariables[variableName].ToString();
            }

            return null;
        }
    }
}
