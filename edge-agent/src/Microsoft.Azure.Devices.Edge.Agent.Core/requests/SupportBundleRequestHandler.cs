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
        public delegate Task<Stream> GetSupportBundle(Option<string> since, Option<string> iothubHostname, Option<bool> edgeRuntimeOnly, CancellationToken token);

        readonly GetSupportBundle getSupportBundle;
        readonly IRequestsUploader requestsUploader;

        public SupportBundleRequestHandler(GetSupportBundle getSupportBundle, IRequestsUploader requestsUploader)
        {
            this.getSupportBundle = getSupportBundle;
            this.requestsUploader = requestsUploader;
        }

        public override string RequestName => "UploadSupportBundle";

        protected override Task<Option<TaskStatusResponse>> HandleRequestInternal(Option<SupportBundleRequest> payloadOption, CancellationToken cancellationToken)
        {
            SupportBundleRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));

            (string correlationId, BackgroundTaskStatus status) = BackgroundTask.Run(
                async () =>
                    {
                        Stream source = await this.getSupportBundle(payload.Since, payload.IothubHostname, payload.EdgeRuntimeOnly, cancellationToken);
                        await this.requestsUploader.UploadSupportBundle(payload.SasUrl, source);
                    },
                "upload support bundle",
                cancellationToken);

            return Task.FromResult(Option.Some(TaskStatusResponse.Create(correlationId, status)));
        }
    }
}
