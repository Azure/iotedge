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
    using Microsoft.Azure.Devices.Edge.Util;

    public class SupportBundleRequestHandler : RequestHandlerBase<SupportBundleRequest, TaskStatusResponse>
    {
        public override string RequestName => "UploadSupportBundle";

        protected override async Task<Option<TaskStatusResponse>> HandleRequestInternal(Option<SupportBundleRequest> payload, CancellationToken cancellationToken)
        {
            await Task.Yield();

            (string correlationId, BackgroundTaskStatus status) = BackgroundTask.Run(() => Task.CompletedTask, "upload logs", cancellationToken);
            return Option.Some(TaskStatusResponse.Create(correlationId, status));
        }
    }
}
