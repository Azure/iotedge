// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LegacyDirectMethodTestResult : TestResultBase
    {
        public LegacyDirectMethodTestResult(
            string source,
            DateTime createdAt,
            string result)
            : base(source, TestOperationResultType.LegacyDirectMethod, createdAt)
        {
            this.Result = Preconditions.CheckNonWhiteSpace(result, nameof(result));
        }

        public string Result { get; set; }

        public override string GetFormattedResult()
        {
            return this.Result;
        }
    }
}
