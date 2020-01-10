// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethodReport
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Newtonsoft.Json;
    using TestResultCoordinator.Reports;

    /// <summary>
    /// This test result comparer is used to determine if 2 DirectMethod TestOperationResult instances matches.
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

            if (!TestOperationResultType.DirectMethod.Equals(value1.Type) || !TestOperationResultType.DirectMethod.Equals(value2.Type))
            {
                throw new InvalidDataException($"Incorrect TestOperationResult Type for comparer {nameof(DirectMethodTestOperationResultComparer)}. Types are: {value1.Type} and {value2.Type}");
            }

            DirectMethodTestResult dmtr1 = JsonConvert.DeserializeObject<DirectMethodTestResult>(value1.Result);
            DirectMethodTestResult dmtr2 = JsonConvert.DeserializeObject<DirectMethodTestResult>(value2.Result);

            return string.Equals(value1.Type, value2.Type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dmtr1.SequenceNumber, dmtr2.SequenceNumber, StringComparison.OrdinalIgnoreCase);
        }
    }
}
