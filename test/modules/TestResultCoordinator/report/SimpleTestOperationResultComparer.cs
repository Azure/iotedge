// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using System;

    /// <summary>
    /// This test result comparer is used to determine if 2 TestOperationResult instances matches.
    /// </summary>
    sealed class SimpleTestOperationResultComparer : ITestResultComparer<TestOperationResult>
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

            return string.Equals(value1.Type, value2.Type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value1.Result, value2.Result, StringComparison.OrdinalIgnoreCase);
        }
    }
}
