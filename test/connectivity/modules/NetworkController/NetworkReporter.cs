// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading.Tasks;

    class NetworkReporter : INetworkStatusReporter
    {
        public Task ReportNetworkStatus(NetworkControllerOperation settingRule, NetworkStatus status, string description, bool success = true)
        {
            // TODO: send to analyzer
            return Task.CompletedTask;
        }
    }
}
