// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;

    interface INetworkStatusReporter
    {
        Task ReportNetworkStatus(NetworkControllerOperation settingRule, NetworkStatus networkStatus, NetworkControllerType networkControllerType, bool success = true);
    }
}
