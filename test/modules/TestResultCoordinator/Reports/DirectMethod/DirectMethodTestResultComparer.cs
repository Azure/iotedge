// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.DirectMethod
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Newtonsoft.Json;
    using TestResultCoordinator.Reports;

    /// <summary>
    /// This test result comparer is used to determine if 2 TestOperationResult instances matches.
    /// </summary>
    sealed class DirectMethodTestOperationResultComparer : ITestResultComparer<TestOperationResult>
    {
        public bool Matches(TestOperationResult value1, TestOperationResult value2)
        {
            if ((value1 == null && value2 != null) || (value1 != null && value2 == null))
            {
                return false;
            }

            if (value1 == null && value2 == null)
            {
                return true;
            }

            DirectMethodTestResult dmResult1 = JsonConvert.DeserializeObject<DirectMethodTestResult>(value1.Result);
            DirectMethodTestResult dmResult2 = JsonConvert.DeserializeObject<DirectMethodTestResult>(value2.Result);

            return dmResult1.TrackingId == dmResult2.TrackingId
                && dmResult1.SequenceNumber == dmResult2.SequenceNumber
                && dmResult1.HttpStatusCode == dmResult2.HttpStatusCode;
        }
    }
}