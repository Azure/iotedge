// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public static class RegistryManagerHelper
    {
        public static async Task<Tuple<string, string>> CreateDevice(string devicePrefix, string iotHubConnectionString, IotHubServiceClient registryManager, bool iotEdgeCapable = false, bool appendGatewayHostName = true, string scope = null)
        {
            string deviceName = devicePrefix + Guid.NewGuid();
            var device = new Device(deviceName)
            {
                Authentication = new AuthenticationMechanism() { Type = ClientAuthenticationType.Sas }
            };

            if (!string.IsNullOrWhiteSpace(scope))
            {
                device.Scope = scope;
            }

            if (iotEdgeCapable)
            {
                device.Capabilities = new ClientCapabilities { IsIotEdge = true };
            }

            device = await registryManager.Devices.CreateAsync(device);
            string deviceConnectionString = GetDeviceConnectionString(device, ConnectionStringHelper.GetHostName(iotHubConnectionString), appendGatewayHostName);

            await Task.Delay(1000);
            return new Tuple<string, string>(deviceName, deviceConnectionString);
        }

        public static async Task<string> CreateModuleIfNotExists(IotHubServiceClient registryManager, string hostname, string deviceId, string moduleId)
        {
            Module module = await registryManager.Modules.GetAsync(deviceId, moduleId);
            if (module == null)
            {
                module = await registryManager.Modules.CreateAsync(new Module(deviceId, moduleId));
            }

            await Task.Delay(1000);

            string moduleConnectionString = GetModuleConnectionString(module, hostname);
            return moduleConnectionString;
        }

        public static string GetModuleConnectionString(Module module, string hostName)
        {
            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            return $"HostName={hostName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }

        public static async Task RemoveDevice(string deviceId, IotHubServiceClient registryManager)
        {
            Device device = await registryManager.Devices.GetAsync(deviceId);
            if (device != null)
            {
                await registryManager.Devices.DeleteAsync(deviceId);
            }
        }

        public static async Task RemoveModule(string deviceId, string moduleId, IotHubServiceClient registryManager)
        {
            var module = await registryManager.Modules.GetAsync(deviceId, moduleId);
            if (module != null)
            {
                await registryManager.Modules.DeleteAsync(deviceId, moduleId);
            }
        }

        public static async Task<string> GetOrCreateModule(IotHubServiceClient registryManager, string hostName, string deviceId, string moduleId)
        {
            Module module = await registryManager.Modules.GetAsync(deviceId, moduleId);
            if (module == null)
            {
                module = await registryManager.Modules.CreateAsync(new Module(deviceId, moduleId));
            }

            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            return $"HostName={hostName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }

        static string GetDeviceConnectionString(Device device, string hostName, bool appendGatewayHostName = true)
        {
            string connectionString = $"HostName={hostName};DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
            if (appendGatewayHostName)
            {
                string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
                connectionString = $"{connectionString};GatewayHostName={gatewayHostname}";
            }

            return connectionString;
        }
    }
}
