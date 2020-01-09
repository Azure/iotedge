// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Newtonsoft.Json;

    /// <summary>
    /// This test result comparer is used to determine if 2 deployment test result matches;
    /// which means all environment varialbe in expected result should be a subset of environment variable in actual result.
    /// </summary>
    sealed class DeploymentTestResultComparer : ITestResultComparer<TestOperationResult>
    {
        public bool Matches(TestOperationResult expected, TestOperationResult actual)
        {
            if ((expected == null && actual != null) || (expected != null && actual == null))
            {
                return false;
            }

            if (expected == null && actual == null)
            {
                return true;
            }

            if (!string.Equals(expected.Type, actual.Type, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            DeploymentTestResult expectedResult = JsonConvert.DeserializeObject<DeploymentTestResult>(expected.Result);
            DeploymentTestResult actualResult = JsonConvert.DeserializeObject<DeploymentTestResult>(actual.Result);

            if (!string.Equals(expectedResult.TrackingId, actualResult.TrackingId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (expectedResult.EnvironmentVariables.Count > actualResult.EnvironmentVariables.Count)
            {
                return false;
            }

            // Actual result should have key and value matched to expected result
            foreach (string key in expectedResult.EnvironmentVariables.Keys)
            {
                if (!actualResult.EnvironmentVariables.ContainsKey(key))
                {
                    return false;
                }

                if (!string.Equals(expectedResult.EnvironmentVariables[key], actualResult.EnvironmentVariables[key], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
