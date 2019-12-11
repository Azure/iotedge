// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;

    sealed class SimpleTestOperationResultComparer : ITestResultComparer<TestOperationResult>
    {
        public bool Matches(TestOperationResult value1, TestOperationResult value2)
        {
            return (string.Equals(value1.Type, value2.Type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value1.Result, value2.Result, StringComparison.OrdinalIgnoreCase));
        }
    }
}
