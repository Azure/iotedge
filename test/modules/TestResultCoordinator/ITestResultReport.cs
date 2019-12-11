// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    /// <summary>
    /// This defines the basic properties of a test result report.
    /// </summary>
    interface ITestResultReport
    {
        string Title { get; }

        string ResultType { get; }

        string ExpectSource { get; }

        string ActualSource { get; }
    }
}
