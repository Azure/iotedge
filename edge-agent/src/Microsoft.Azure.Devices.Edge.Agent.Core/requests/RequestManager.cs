// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;

    public class RequestManager : IRequestManager
    {
        static readonly IDictionary<string, IRequestHandler> RequestHandlers = new Dictionary<string, IRequestHandler>
        {
            ["ping"] = new PingRequestHandler()
        };

        public async Task<(int, string)> ProcessRequest(string request, string payloadJson)
        {
            try
            {
                if (!RequestHandlers.TryGetValue(request, out IRequestHandler requestHandler))
                {
                    string supportedCommands = string.Join(",", RequestHandlers.Keys);
                    string message = $"Command {request} not found. The supported commands are {supportedCommands}";
                    throw new ArgumentException(message);
                }

                return (200, await requestHandler.HandleRequest(payloadJson));
            }
            catch (Exception ex)
            {
                return GetErrorResponse(ex);
            }
        }

        static (int, string) GetErrorResponse(Exception ex)
        {
            switch (ex)
            {
                case ArgumentException _:
                    return (400, GetErrorPayload(ex.Message));
                default:
                    return (500, GetErrorPayload(ex.Message));
            }
        }

        static string GetErrorPayload(string message)
        {
            var errorPayload = new
            {
                message
            };
            string json = errorPayload.ToJson();
            return json;
        }
    }    
}
