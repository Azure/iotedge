// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class TestOperationResult
    {
        public TestOperationResult(
            string source,
            TestResultType resultType,
            string result,
            DateTime createdOn)
        {
            Preconditions.CheckArgument(resultType != TestResultType.Undefined, nameof(resultType));

            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.ResultType = resultType;
            this.Result = Preconditions.CheckNonWhiteSpace(result, nameof(result));
            this.CreatedOn = createdOn;
        }

        public string Source { get; }

        public TestResultType ResultType { get; }

        public string Result { get; }

        public DateTime CreatedOn { get; }
    }
}
