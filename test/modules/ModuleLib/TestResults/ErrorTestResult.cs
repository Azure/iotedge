// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;

    public class ErrorTestResult : TestResultBase
    {
        public ErrorTestResult(string trackingId, string errorSource, Exception ex)
            : this(trackingId, ex.Message, errorSource, DateTime.UtcNow)
        {
        }

        public ErrorTestResult(string trackingId, string errorSource, string errorMessage, DateTime createdAt)
            : base(TestConstants.Error.TestResultSource, TestOperationResultType.Error, createdAt)
        {
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ErrorSource = Preconditions.CheckNonWhiteSpace(errorSource, nameof(errorSource));
            this.ErrorMessage = Preconditions.CheckNonWhiteSpace(errorMessage, nameof(errorMessage));
        }

        public string TrackingId { get; }

        public string ErrorSource { get; }

        public string ErrorMessage { get; }

        public override string GetFormattedResult() => this.ToPrettyJson();
    }
}
