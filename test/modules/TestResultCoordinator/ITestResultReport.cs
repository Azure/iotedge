// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    interface ITestResultReport
    {
        string Title { get; }

        string ResultType { get; }

        string ExpectSource { get; }

        string ActualSource { get; }
    }
}
