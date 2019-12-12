// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading.Tasks;

    interface INetworkStatusReporter
    {
        Task ReportNetworkStatus(NetworkControllerOperation settingRule, NetworkStatus status, string description, bool success = true);
    }
}
