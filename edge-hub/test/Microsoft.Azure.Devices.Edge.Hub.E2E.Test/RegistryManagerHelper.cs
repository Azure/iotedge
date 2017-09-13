// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Threading.Tasks;
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

        public static string GetDeviceConnectionString(Device device, string hostName)
        {
            string gatewayHostname = ConfigHelper.TestConfig["GatewayHostname"];
            return $"HostName={hostName};DeviceId={device.Id};SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={gatewayHostname}";
        }

        public static Task RemoveDevice(string deviceName, RegistryManager registryManager)
        {
            return registryManager.RemoveDeviceAsync(deviceName);
        }
    }
}