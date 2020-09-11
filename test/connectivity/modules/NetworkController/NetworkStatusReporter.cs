// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    class NetworkStatusReporter : INetworkStatusReporter
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<NetworkStatusReporter>();
        readonly TestResultReportingClient testResultReportingClient;
        readonly string moduleId;
        readonly string trackingId;

        public NetworkStatusReporter(Uri testResultCoordinatorEndpoint, string moduleId, string trackingId)
        {
            this.testResultReportingClient = new TestResultReportingClient() { BaseUrl = testResultCoordinatorEndpoint.AbsoluteUri };
            this.moduleId = moduleId;
            this.trackingId = trackingId;
        }

        public Task ReportNetworkStatusAsync(NetworkControllerOperation operation, NetworkControllerStatus networkControllerStatus, NetworkControllerType networkControllerType, bool success = true)
        {
            var testResult = new NetworkControllerTestResult(this.moduleId, DateTime.UtcNow)
            {
                Operation = operation,
                OperationStatus = success ? "Success" : "Failed",
                NetworkControllerType = networkControllerType,
                NetworkControllerStatus = networkControllerStatus,
                TrackingId = this.trackingId
            };

            return ExecuteWithRetry(
                () => ModuleUtil.ReportTestResultAsync(
                    this.testResultReportingClient, Log, testResult),
                RetryingReportTestResult);
        }

        static Task ExecuteWithRetry(Func<Task> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = RetryPolicy.DefaultExponential;
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        static void RetryingReportTestResult(RetryingEventArgs retryingEventArgs)
        {
            Log.LogDebug($"Retrying ReportTestResult {retryingEventArgs.CurrentRetryCount} times because of error - {retryingEventArgs.LastException}");
        }
    }
}
