// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;

    public static class RegistryManagerHelper
    {
        public static async Task<Tuple<string, string>> CreateDevice(string devicePrefix, string iotHubConnectionString, RegistryManager registryManager, bool iotEdgeCapable = false, bool appendGatewayHostName = true)
        {
            string deviceName = devicePrefix + Guid.NewGuid();
            var device = new Device(deviceName)
            {                
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };

            if(iotEdgeCapable)
            {
                device.Capabilities = new DeviceCapabilities { IotEdge = true };
            }

            device = await registryManager.AddDeviceAsync(device);
            string deviceConnectionString = GetDeviceConnectionString(device, ConnectionStringHelper.GetHostName(iotHubConnectionString), appendGatewayHostName);

            await Task.Delay(1000);
            return new Tuple<string, string>(deviceName, deviceConnectionString);
        }

        public static async Task<string> CreateModuleIfNotExists(RegistryManager registryManager, string hostname, string deviceId, string moduleId)
        {
            Module module = await registryManager.GetModuleAsync(deviceId, moduleId);
            if (module == null)
            {
                module = await registryManager.AddModuleAsync(new Module(deviceId, moduleId));
            }

            await Task.Delay(1000);

            string moduleConnectionString = GetModuleConnectionString(module, hostname);
            return moduleConnectionString;
        }

        static string GetDeviceConnectionString(Device device, string hostName, bool appendGatewayHostName = true)
        {            
            string connectionString = $"HostName={hostName};DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
            if(appendGatewayHostName)
            {
                string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
                connectionString = $"{connectionString};GatewayHostName={gatewayHostname}";
            }
            return connectionString;
        }

        public static string GetModuleConnectionString(Module module, string hostName)
        {
            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            return $"HostName={hostName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }

        public static async Task RemoveDevice(string deviceId, RegistryManager registryManager)
        {
            Device device = await registryManager.GetDeviceAsync(deviceId);
            if (device != null)
            {
                await registryManager.RemoveDeviceAsync(deviceId);
            }
        }

        public static async Task<string> GetOrCreateModule(RegistryManager registryManager, string hostName, string deviceId, string moduleId)
        {
            Module module = await registryManager.GetModuleAsync(deviceId, moduleId);
            if (module == null)
            {
                module = await registryManager.AddModuleAsync(new Module(deviceId, moduleId));
            }

            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            return $"HostName={hostName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }
    }
}
