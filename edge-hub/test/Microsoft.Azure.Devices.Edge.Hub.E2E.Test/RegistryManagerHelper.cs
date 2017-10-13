// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public static class RegistryManagerHelper
    {
        public static async Task<Tuple<string, string>> CreateDevice(string devicePrefix, string iotHubConnectionString, RegistryManager registryManager)
        {
            string deviceName = devicePrefix + Guid.NewGuid();
            Device device = await registryManager.AddDeviceAsync(new Device(deviceName));
            string deviceConnectionString = GetDeviceConnectionString(device, ConnectionStringHelper.GetHostName(iotHubConnectionString));

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

        public static string GetDeviceConnectionString(Device device, string hostName)
        {
            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            return $"HostName={hostName};DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }

        public static string GetModuleConnectionString(Module module, string hostName)
        {
            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            return $"HostName={hostName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }

        public static Task RemoveDevice(string deviceName, RegistryManager registryManager)
        {
            return registryManager.RemoveDeviceAsync(deviceName);
        }

        public static async Task<string> GetOrCreateModule(RegistryManager registryManager, string hostName, string deviceId, string moduleId)
        {
            Module module = await registryManager.GetModuleAsync(deviceId, moduleId);
            if (module == null)
            {
                module = await registryManager.AddModuleAsync(new Module(deviceId, moduleId));
            }

            string gatewayHostname = "127.0.0.1";
            return $"HostName={hostName};DeviceId={module.DeviceId};ModuleId={module.Id};SharedAccessKey={module.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }
    }
}