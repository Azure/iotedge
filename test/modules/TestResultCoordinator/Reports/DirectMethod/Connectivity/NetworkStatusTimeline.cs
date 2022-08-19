// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.Connectivity
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
    using TestResultCoordinator.Reports;

    class NetworkStatusTimeline
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(NetworkStatusTimeline));

        readonly List<NetworkControllerTestResult> networkControllerTestResults;
        readonly TimeSpan tolerancePeriod;
        bool testResultsValidated = false;
        NetworkControllerStatus initialNetworkControllerStatus;

        public static async Task<NetworkStatusTimeline> CreateAsync(
            IAsyncEnumerator<TestOperationResult> networkControllerTestOperationResults,
            TimeSpan tolerancePeriod,
            NetworkControllerStatus initialNetworkControllerStatus = NetworkControllerStatus.Disabled)
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

            return new NetworkStatusTimeline(networkControllerTestResults, tolerancePeriod, initialNetworkControllerStatus);
        }

        NetworkStatusTimeline(List<NetworkControllerTestResult> networkControllerTestResults, TimeSpan tolerancePeriod, NetworkControllerStatus initialNetworkControllerStatus)
        {
            if (networkControllerTestResults?.Count == 0)
            {
                throw new ArgumentException("Network Controller Test Results is empty.");
            }

            if (networkControllerTestResults?.Count % 2 != 0)
            {
                throw new ArgumentException("Network Controller Test Results must have an even number of results.");
            }

            this.networkControllerTestResults = networkControllerTestResults;
            this.networkControllerTestResults.Sort(new NetworkTimelineResultComparer());
            this.tolerancePeriod = tolerancePeriod;
            this.initialNetworkControllerStatus = initialNetworkControllerStatus;
        }

        private void ValidateNetworkControllerTestResults()
        {
            if (this.testResultsValidated)
            {
                return;
            }

            for (int i = 0; i < this.networkControllerTestResults.Count; i += 2)
            {
                NetworkControllerTestResult curr = this.networkControllerTestResults[i];
                if (!NetworkControllerOperation.SettingRule.Equals(curr.Operation))
                {
                    throw new InvalidOperationException("Expected SettingRule.");
                }

                if (!NetworkControllerOperation.RuleSet.Equals(this.networkControllerTestResults[i + 1].Operation))
                {
                    throw new InvalidOperationException("Test result SettingRule found with no RuleSet found after.");
                }

                if (!curr.NetworkControllerStatus.Equals(this.networkControllerTestResults[i + 1].NetworkControllerStatus))
                {
                    throw new InvalidOperationException("Test result SettingRule and following RuleSet do not match NetwokControllerStatuses");
                }
            }

            this.testResultsValidated = true;
        }

        public (NetworkControllerStatus networkControllerStatus, bool isWithinTolerancePeriod, TimeSpan delay) GetNetworkControllerStatusAndWithinToleranceAt(DateTime statusTime)
        {
            this.ValidateNetworkControllerTestResults();
            // the time between when the test results finish and the network controller comes up
            var delay = TimeSpan.Zero;

            // Return network controller status at given time
            NetworkControllerStatus networkControllerStatus = this.initialNetworkControllerStatus;
            bool isWithinTolerancePeriod = false;
            for (int i = 0; i < this.networkControllerTestResults.Count; i += 2)
            {
                NetworkControllerTestResult curr = this.networkControllerTestResults[i];
                if (statusTime <= curr.CreatedAt)
                {
                    break;
                }

                networkControllerStatus = curr.NetworkControllerStatus;
                NetworkControllerTestResult next = this.networkControllerTestResults[i + 1];
                isWithinTolerancePeriod = statusTime > curr.CreatedAt && statusTime <= next.CreatedAt.Add(this.tolerancePeriod);
                delay = statusTime.Subtract(curr.CreatedAt);
            }

            return (networkControllerStatus, isWithinTolerancePeriod, delay);
        }

        static Option<NetworkControllerTestResult> GetNetworkControllerTestOperationResult(TestOperationResult current)
        {
            if (!current.Type.Equals(TestOperationResultType.Network.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Option.None<NetworkControllerTestResult>();
            }

            Logger.LogDebug($"Deserializing for source {current.Source} result: {current.Result} {current.Type}");
            NetworkControllerTestResult networkControllerTestResult = JsonConvert.DeserializeObject<NetworkControllerTestResult>(current.Result);
            return Option.Some(networkControllerTestResult);
        }
    }

    public class NetworkTimelineResultComparer : IComparer<NetworkControllerTestResult>
    {
        public int Compare(NetworkControllerTestResult n1, NetworkControllerTestResult n2) => n1.CreatedAt.CompareTo(n2.CreatedAt);
    }
}
