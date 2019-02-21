// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;

    abstract class RequestHandlerBase<TU, TV> : IRequestHandler
        where TU : class
        where TV : class
    {
        public async Task<string> HandleRequest(string payloadJson)
        {
            try
            {
                TU payload = payloadJson?.FromJson<TU>();
                TV result = await this.HandleRequestInternal(payload);
                string responseJson = result?.ToJson() ?? string.Empty;
                return responseJson;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error handling request - {e}");
                return string.Empty;
            }
        }

        protected abstract Task<TV> HandleRequestInternal(TU payloadJson);
    }
}
