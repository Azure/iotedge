// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is basic test result report implementation.
    /// </summary>
    abstract class TestResultReportBase : ITestResultReport
    {
        protected TestResultReportBase(string expectSource, string actualSource, string resultType)
        {
            this.ExpectSource = Preconditions.CheckNonWhiteSpace(expectSource, nameof(expectSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        public string Title => $"Counting Report ({this.ResultType}) between [{this.ExpectSource}] and [{this.ActualSource}]";

        public string ResultType { get; }

        public string ExpectSource { get; }

        public string ActualSource { get; }
    }
}
