// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;

    public class LegacyDirectMethodTestResult : TestResultBase
    {
        public LegacyDirectMethodTestResult(string source, DateTime createdAt) :
            base(source, TestOperationResultType.LegacyDirectMethod, createdAt)
        {
        }

        public string Result { get; set; }

        public override string GetFormattedResult()
        {
            return this.Result;
        }
    }
}
