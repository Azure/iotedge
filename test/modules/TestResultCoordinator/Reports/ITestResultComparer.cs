// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    /// <summary>
    /// It is used to compare 2 test results whether they matches or not.
    /// </summary>
    /// <typeparam name="T">Type of test result.</typeparam>
    interface ITestResultComparer<T>
    {
        bool Matches(T expected, T actual);
    }
}
