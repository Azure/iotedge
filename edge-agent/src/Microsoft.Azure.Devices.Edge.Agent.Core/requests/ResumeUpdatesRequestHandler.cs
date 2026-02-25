// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ResumeUpdatesRequestHandler : RequestHandlerBase<object, object>
    {
        readonly ISuspendManager suspendManager;

        public override string RequestName => "ResumeUpdates";

        public ResumeUpdatesRequestHandler(ISuspendManager suspendManager)
        {
            this.suspendManager = Preconditions.CheckNotNull(suspendManager, nameof(suspendManager));
        }

        protected override async Task<Option<object>> HandleRequestInternal(Option<object> payload, CancellationToken token)
        {
            await this.suspendManager.ResumeUpdatesAsync(token);

            return Option.None<object>();
        }

        protected override Option<object> ParsePayload(Option<string> payloadJson)
            => Option.None<object>();
    }
}
