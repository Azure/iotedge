// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    /// <summary>
    /// It is used to compare 2 test results whether they matches or not.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    interface ITestResultComparer<T>
    {
        bool Matches(T value1, T value2);
    }
}
