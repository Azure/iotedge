// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class SupportBundleRequestHandler : RequestHandlerBase<SupportBundleRequest, TaskStatusResponse>
    {
        public delegate Task<Stream> GetSupportBundle(Option<string> since, Option<string> until, Option<string> iothubHostname, Option<bool> edgeRuntimeOnly, CancellationToken token);

        readonly GetSupportBundle getSupportBundle;
        readonly IRequestsUploader requestsUploader;
        readonly string iotHubHostName;
        readonly TimeSpan supportTaskTimeout;

        public SupportBundleRequestHandler(GetSupportBundle getSupportBundle, IRequestsUploader requestsUploader, string iotHubHostName, TimeSpan supportTaskTimeout)
        {
            this.getSupportBundle = getSupportBundle;
            this.requestsUploader = requestsUploader;
            this.iotHubHostName = iotHubHostName;
            this.supportTaskTimeout = supportTaskTimeout;
        }

        public override string RequestName => "UploadSupportBundle";

        protected override Task<Option<TaskStatusResponse>> HandleRequestInternal(Option<SupportBundleRequest> payloadOption, CancellationToken cancellationToken)
        {
            SupportBundleRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));

            CancellationTokenSource supportCts = new CancellationTokenSource(this.supportTaskTimeout);
            (string correlationId, BackgroundTaskStatus status) = BackgroundTask.Run(
                async () =>
                    {
                        Stream source = await this.getSupportBundle(payload.Since, payload.Until, Option.Maybe(this.iotHubHostName), payload.EdgeRuntimeOnly, CancellationToken.None);
                        await this.requestsUploader.UploadSupportBundle(payload.SasUrl, source);
                    },
                "upload support bundle",
                supportCts.Token);

            return Task.FromResult(Option.Some(TaskStatusResponse.Create(correlationId, status)));
        }
    }
}
