// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    /// <summary>
    /// This defines the basic properties of a test result report.
    /// </summary>
    public interface ITestResultReport
    {
        string TestDescription { get; }

        string TrackingId { get; }

        string Title { get; }

        string ResultType { get; }

        bool IsPassed { get; }
    }
}
