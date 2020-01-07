// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report.DirectMethodReport
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class NetworkStatusTimeline
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(NetworkStatusTimeline));

        readonly IReadOnlyList<NetworkControllerTestResult> networkControllerTestResults;
        readonly TimeSpan tolerancePeriod;

        public static async Task<NetworkStatusTimeline> Create(ITestResultCollection<TestOperationResult> networkControllerTestOperationResults, TimeSpan tolerancePeriod)
        {
            List<NetworkControllerTestResult> networkControllerTestResults = new List<NetworkControllerTestResult>();

            while (await networkControllerTestOperationResults.MoveNextAsync())
            {
                Option<NetworkControllerTestResult> networkControllerTestResult = GetNetworkControllerTestOperationResult(networkControllerTestOperationResults.Current);

                networkControllerTestResult.ForEach(
                    r =>
                    {
                        networkControllerTestResults.Add(r);
                    });
            }

            return new NetworkStatusTimeline(networkControllerTestResults, tolerancePeriod);
        }

        NetworkStatusTimeline(IReadOnlyList<NetworkControllerTestResult> networkControllerTestResults, TimeSpan tolerancePeriod)
        {
            this.networkControllerTestResults = networkControllerTestResults;
            this.tolerancePeriod = tolerancePeriod;
        }

        public NetworkControllerStatus GetNetworkControllerStatusAt(DateTime dateTime)
        {
            // Return network controller status at given time
            NetworkControllerStatus networkControllerStatus = NetworkControllerStatus.Enabled;
            for (int i = 0;  i < this.networkControllerTestResults.Count; i++)
            {
                // If given DateTime is temporally after a DateTime on the list, set the networkStatus to that
                if (dateTime >= this.networkControllerTestResults[i].CreatedAt)
                {
                    networkControllerStatus = this.networkControllerTestResults[i].NetworkControllerStatus;
                }
            }

            return networkControllerStatus;
        }

        public bool IsWithinTolerancePeriod(DateTime dateTime)
        {
            // Return true if given dateTime is within a tolerance period of one of the NetworkControllerTestResults
            foreach (NetworkControllerTestResult networkControllerTestResult in this.networkControllerTestResults)
            {
                // If dateTime is after a TestResult's CreatedAt time, but before that TestResult's CreatedAt time + tolerance period, then dateTime is within the tolerance period
                if (dateTime > networkControllerTestResult.CreatedAt && dateTime <= networkControllerTestResult.CreatedAt.Add(this.tolerancePeriod))
                {
                    return true;
                }
            }

            return false;
        }

        static Option<NetworkControllerTestResult> GetNetworkControllerTestOperationResult(TestOperationResult current)
        {
            if (!current.Type.Equals(TestOperationResultType.Network.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                Option.None<TwinTestResult>();
            }

            Logger.LogDebug($"Deserializing for source {current.Source} result: {current.Result} {current.Type}");
            NetworkControllerTestResult twinTestResult = JsonConvert.DeserializeObject<NetworkControllerTestResult>(current.Result);
            return Option.Some(twinTestResult);
        }
    }
}
