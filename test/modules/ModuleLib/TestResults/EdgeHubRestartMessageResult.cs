// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeHubRestartMessageResult : MessageTestResult
    {
        public EdgeHubRestartMessageResult(
            string source,
            DateTime createdAt,
            DateTime edgeHubRestartedTime,
            HttpStatusCode edgeHubRestartStatusCode,
            DateTime messageCompletedTime,
            HttpStatusCode messageCompletedStatusCode)
            : base(source, createdAt)
        {
            this.EdgeHubRestartedTime = Preconditions.CheckNotNull(edgeHubRestartedTime, nameof(edgeHubRestartedTime));
            this.EdgeHubRestartStatusCode = Preconditions.CheckNotNull(edgeHubRestartStatusCode, nameof(edgeHubRestartStatusCode));
            this.MessageCompletedTime = Preconditions.CheckNotNull(messageCompletedTime, nameof(messageCompletedTime));
            this.MessageCompletedStatusCode = Preconditions.CheckNotNull(messageCompletedStatusCode, nameof(messageCompletedStatusCode));
        }

        DateTime EdgeHubRestartedTime { get; set; }

        public HttpStatusCode EdgeHubRestartStatusCode { get; set; }

        public DateTime MessageCompletedTime { get; set; }

        public HttpStatusCode MessageCompletedStatusCode { get; set; }

        public string GetMessageTestResult()
        {
            return base.GetFormattedResult();
        }

        public override string GetFormattedResult()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
