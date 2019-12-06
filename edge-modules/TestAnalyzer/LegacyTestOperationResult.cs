// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestAnalyzer.Controllers;

    public class LegacyTestOperationResult
    {
        private LegacyTestOperationResult(string moduleId, string statusCode, DateTime responseDateTime)
        {
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.StatusCode = Preconditions.CheckNonWhiteSpace(statusCode, nameof(statusCode));
            this.ResponseDateTime = responseDateTime;
        }

        public string ModuleId { get; }

        public string StatusCode { get; }

        public DateTime ResponseDateTime { get; }

        public static LegacyTestOperationResult Convert(TestOperationResult result)
        {
            if (result.ResultType != TestResultType.LegacyDirectMethod && result.ResultType != TestResultType.LegacyTwin)
            {
                throw new NotSupportedException($"result type is {result.ResultType}; only Legacy result types are supported.");
            }

            return new LegacyTestOperationResult(result.Source, result.Result, result.CreatedOn);
        }
    }
}
