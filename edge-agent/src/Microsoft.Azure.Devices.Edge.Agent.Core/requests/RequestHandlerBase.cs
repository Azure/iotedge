// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public abstract class RequestHandlerBase<TU, TV> : IRequestHandler
        where TU : class
        where TV : class
    {
        public abstract string RequestName { get; }

        public async Task<Option<string>> HandleRequest(Option<string> payloadJson)
        {
            Option<TU> payload = this.ParsePayload(payloadJson);
            Option<TV> result = await this.HandleRequestInternal(payload);
            Option<string> responseJson = result.Map(r => r.ToJson());
            return responseJson;
        }

        protected virtual Option<TU> ParsePayload(Option<string> payloadJson)
        {
            try
            {
                return payloadJson.Map(p => p.FromJson<TU>());
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Error parsing command payload because of error - {ex.Message}");
            }
        }

        protected abstract Task<Option<TV>> HandleRequestInternal(Option<TU> payload);
    }
}
