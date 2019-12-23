// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;

    interface INetworkStatusReporter
    {
        Task ReportNetworkStatus(NetworkControllerOperation settingRule, NetworkControllerStatus networkControllerStatus, NetworkControllerType networkControllerType, bool success = true);
    }
}
