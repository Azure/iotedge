// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Net;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;

    static class LinuxTrafficControllerHelper
    {
        public static string CommandName => "tc";

        public static string GetRemoveAllArguments(string networkInterfaceName) => $"qdisc delete dev {networkInterfaceName} root";

        public static string GetRootRule(string networkInterfaceName) => $"qdisc add dev {networkInterfaceName} root handle 1: prio priomap 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0";

        public static string GetShowRules(string networkInterfaceName) => $"qdisc show dev {networkInterfaceName}";

        public static string GetIpFilter(string networkInterfaceName, IPAddress[] iothubAddresses) => $"filter add dev {networkInterfaceName} parent 1:0 protocol ip u32 match ip src {iothubAddresses[0]} flowid 1:2";

        public static string GetNetworkEmulatorAddRule(string networkInterfaceName, NetworkProfileSetting settings) => $"qdisc add dev {networkInterfaceName} parent 1:2 handle 20: netem loss {settings.PackageLoss}% delay {settings.Delay}ms {settings.Jitter}ms rate {settings.Bandwidth}{settings.BandwidthUnit}";
    }
}
