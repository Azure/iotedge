// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper
{
    using System;
    using System.Collections;
    using Microsoft.Azure.Devices.Client;

    public class DeviceClientFactory
    {
        const string EdgeletUriVariableName = "IOTEDGE_IOTEDGEDURI";
        const string EdgeletApiVersionVariableName = "IOTEDGE_IOTEDGEDVERSION";
        const string IotHubHostnameVariableName = "IOTEDGE_IOTHUBHOSTNAME";
        const string GatewayHostnameVariableName = "IOTEDGE_GATEWAYHOSTNAME";
        const string DeviceIdVariableName = "IOTEDGE_DEVICEID";
        const string ModuleIdVariableName = "IOTEDGE_MODULEID";
        const string AuthSchemeVariableName = "IOTEDGE_AUTHSCHEME";
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
                string hostname = this.GetValueFromEnvironment(envVariables, IotHubHostnameVariableName) ?? throw new InvalidOperationException($"Environement variable {IotHubHostnameVariableName} is required.");
                string authScheme = this.GetValueFromEnvironment(envVariables, AuthSchemeVariableName) ?? throw new InvalidOperationException($"Environement variable {AuthSchemeVariableName} is required.");
                string gateway = this.GetValueFromEnvironment(envVariables, GatewayHostnameVariableName);
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
            if (this.transportSettings != null)
            {
                return DeviceClient.Create(hostname, gateway, authMethod, this.transportSettings);
            }

            if (this.transportType.HasValue)
            {
                return DeviceClient.Create(hostname, gateway, authMethod, this.transportType.Value);
            }

            return DeviceClient.Create(hostname, gateway, authMethod);
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
