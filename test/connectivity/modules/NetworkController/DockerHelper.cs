// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Net.NetworkInformation;
    using Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class DockerHelper
    {
        const int DockerNetworkInterfaceNameLenght = 12;
        const int DockerNetworkInterfaceStartIndex = 0;
        const string DockerNetworkInterfaceSuffix = "br-";
        static readonly ILogger Log = Logger.Factory.CreateLogger<DockerHelper>();

        public static Option<string> GetDockerInterfaceName()
        {
            var client = new DockerClientConfiguration(new Uri(Settings.Current.DockerUri)).CreateClient();
            var networkId = client.Networks.InspectNetworkAsync(Settings.Current.NetworkId).Result.ID;

            string interfaceName = GetInterfaceNameFromNetworkId(networkId);

            Log.LogInformation($"{Settings.Current.NetworkId} network has id {networkId}");

            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.Name.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogInformation($"Found network interface {item.Name}");
                    return Option.Some(item.Name);
                }
            }

            Log.LogInformation($"No network interface found for docker network id {Settings.Current.NetworkId}");
            return Option.None<string>();
        }

        static string GetInterfaceNameFromNetworkId(string networkId)
        {
            // TODO: this is on linux, windows might be different
            // Network interface name has by default first chars form networkId
            return string.Format($"{DockerNetworkInterfaceSuffix}{networkId.Substring(DockerNetworkInterfaceStartIndex, DockerNetworkInterfaceNameLenght)}");
        }
    }
}
