// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report.DirectMethodReport
{
    using System;
    using System.Collections;
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
        bool testResultsValidated = false;

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

        NetworkStatusTimeline(List<NetworkControllerTestResult> networkControllerTestResults, TimeSpan tolerancePeriod)
        {
            networkControllerTestResults.Sort(new NetworkTimelineResultComparer());
            this.networkControllerTestResults = networkControllerTestResults;
            this.tolerancePeriod = tolerancePeriod;
        }

        private void ValidateNetworkControllerTestResults()
        {
            for (int i = 0; i < this.networkControllerTestResults.Count; i++)
            {
                NetworkControllerTestResult curr = this.networkControllerTestResults[i];
                if (NetworkControllerOperation.SettingRule.Equals(curr.Operation))
                {
                    if (i + 1 >= this.networkControllerTestResults.Count || !NetworkControllerOperation.RuleSet.Equals(this.networkControllerTestResults[i + 1].Operation))
                    {
                        throw new InvalidOperationException("Test result SettingRule found with no RuleSet found after.");
                    }

                    if (!curr.NetworkControllerStatus.Equals(this.networkControllerTestResults[i + 1].NetworkControllerStatus))
                    {
                        throw new InvalidOperationException("Test result SettingRule and following RuleSet do not match NetwokControllerStatuses");
                    }
                    i++;
                }
                else
                {
                    throw new InvalidOperationException("Expected SettingRule");
                }
            }
            this.testResultsValidated = true;
        }

        public (NetworkControllerStatus, bool) GetNetworkControllerStatusAndWithinToleranceAt(DateTime dateTime)
        {
            if (this.testResultsValidated == false)
            {
                this.ValidateNetworkControllerTestResults();
            }
            // Return network controller status at given time
            NetworkControllerStatus networkControllerStatus = NetworkControllerStatus.Unknown;
            bool isWithinTolerancePeriod = false;
            for (int i = 0;  i < this.networkControllerTestResults.Count; i++)
            {
                NetworkControllerTestResult curr = this.networkControllerTestResults[i];
                if (dateTime <= curr.CreatedAt)
                {
                    break;
                }

                networkControllerStatus = curr.NetworkControllerStatus;
                NetworkControllerTestResult next = this.networkControllerTestResults[i + 1];
                isWithinTolerancePeriod = dateTime >= curr.CreatedAt && dateTime <= next.CreatedAt.Add(this.tolerancePeriod);
                i++;
            }

            if (NetworkControllerStatus.Unknown.Equals(networkControllerStatus))
            {
                throw new InvalidOperationException("No network controller status found for this time period.");
            }

            return (networkControllerStatus, isWithinTolerancePeriod);
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

    public class NetworkTimelineResultComparer : IComparer<NetworkControllerTestResult>
    {
        public int Compare(NetworkControllerTestResult n1, NetworkControllerTestResult n2) => n1.CreatedAt.CompareTo(n2.CreatedAt);
    }
}
