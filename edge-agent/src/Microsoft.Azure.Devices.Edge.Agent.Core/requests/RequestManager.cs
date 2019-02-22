// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RequestManager : IRequestManager
    {
        static readonly IDictionary<string, IRequestHandler> DefaultRequestHandlers = new ReadOnlyDictionary<string, IRequestHandler>(
            new Dictionary<string, IRequestHandler>()
        {
            ["ping"] = new PingRequestHandler()
        });

        readonly IDictionary<string, IRequestHandler> requestHandlers;

        public RequestManager()
            : this(DefaultRequestHandlers)
        {
        }

        internal RequestManager(IDictionary<string, IRequestHandler> requestHandlers)
        {
            this.requestHandlers = Preconditions.CheckNotNull(requestHandlers, nameof(requestHandlers));
        }

        public async Task<(int, string)> ProcessRequest(string request, string payloadJson)
        {
            try
            {
                Preconditions.CheckNonWhiteSpace(request, nameof(request));                
                if (!this.requestHandlers.TryGetValue(request, out IRequestHandler requestHandler))
                {
                    string supportedCommands = string.Join(",", this.requestHandlers.Keys);
                    string message = $"Command '{request}' not found. The supported commands are - {supportedCommands}";
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
