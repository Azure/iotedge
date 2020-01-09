// Copyright (c) Microsoft. All rights reserved.
using System;
using Microsoft.Azure.Devices.Edge.Util;

namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    public abstract class TestResultBase
    {
        public TestResultBase(string source, TestOperationResultType resultType, DateTime createdAt)
        {
            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.ResultType = resultType;
            this.CreatedAt = createdAt;
        }

        public string Source { get; private set; }

        public TestOperationResultType ResultType { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public abstract string GetFormattedResult();

        public TestOperationResultDto ToTestOperationResultDto()
        {
            return new TestOperationResultDto { Source = this.Source, Type = this.ResultType.ToString(), Result = this.GetFormattedResult(), CreatedAt = this.CreatedAt };
        }

        public TestOperationResult ToTestOperationResult()
        {
            return new TestOperationResult(this.Source, this.ResultType.ToString(), this.GetFormattedResult(), this.CreatedAt);
        }
    }
}
