// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;

    interface INetworkStatusReporter
    {
        Task ReportNetworkStatusAsync(NetworkControllerOperation settingRule, NetworkControllerStatus networkControllerStatus, NetworkControllerType networkControllerType, bool success = true);
    }
}
