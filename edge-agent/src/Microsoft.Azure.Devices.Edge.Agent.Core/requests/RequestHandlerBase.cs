// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    abstract class RequestHandlerBase<TU, TV> : IRequestHandler
        where TU : class
        where TV : class
    {
        public async Task<string> HandleRequest(string payloadJson)
        {
            TU payload = this.ParsePayload(payloadJson);
            TV result = await this.HandleRequestInternal(payload);
            string responseJson = result?.ToJson() ?? string.Empty;
            return responseJson;
        }

        protected virtual TU ParsePayload(string payloadJson)
        {
            Preconditions.CheckNonWhiteSpace(payloadJson, nameof(payloadJson));
            try
            {
                return payloadJson.FromJson<TU>();
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Error parsing command payload because of error - {ex.Message}");
            }
        }

        protected abstract Task<TV> HandleRequestInternal(TU payload);
    }
}
