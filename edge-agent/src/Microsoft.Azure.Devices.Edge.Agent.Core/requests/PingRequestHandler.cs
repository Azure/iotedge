// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PingRequestHandler : RequestHandlerBase<object, object>
    {
        public override string RequestName => "ping";

        protected override Task<Option<object>> HandleRequestInternal(Option<object> payload)
            => Task.FromResult(Option.None<object>());

        protected override Option<object> ParsePayload(Option<string> payloadJson)
            => Option.None<object>();
    }
}
