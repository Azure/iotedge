// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

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
                Events.HandlingRequest(request, payloadJson);
                if (!this.requestHandlers.TryGetValue(request, out IRequestHandler requestHandler))
                {
                    string supportedCommands = string.Join(",", this.requestHandlers.Keys);
                    string message = $"Command '{request}' not found. The supported commands are - {supportedCommands}";
                    throw new ArgumentException(message);
                }

                string responsePayload = await requestHandler.HandleRequest(payloadJson);
                Events.HandledRequest(request);
                return ((int)HttpStatusCode.OK, responsePayload);
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingRequest(request, ex);
                return GetErrorResponse(ex);
            }
        }

        static (int, string) GetErrorResponse(Exception ex)
        {
            switch (ex)
            {
                case ArgumentException _:
                    return ((int)HttpStatusCode.BadRequest, GetErrorPayload(ex.Message));
                default:
                    return ((int)HttpStatusCode.InternalServerError, GetErrorPayload(ex.Message));
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

        static class Events
        {
            const int IdStart = AgentEventIds.RequestManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RequestManager>();

            enum EventIds
            {
                ScheduledModule = IdStart + 1,
                HandlingRequest,
                ErrorHandlingRequest
            }

            public static void ScheduledModule(IRuntimeModule module, TimeSpan elapsedTime, TimeSpan coolOffPeriod)
            {
                TimeSpan timeLeft = coolOffPeriod - elapsedTime;
                Log.LogInformation(
                    (int)EventIds.ScheduledModule,
                    $"Module '{module.Name}' scheduled to restart after {coolOffPeriod.Humanize()} ({timeLeft.Humanize()} left).");
            }

            public static void ErrorHandlingRequest(string request, Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingRequest, exception, $"Error handling request {request}");
            }

            public static void HandlingRequest(string request, string payloadJson)
            {
                Log.LogInformation(
                    (int)EventIds.HandlingRequest,
                    string.IsNullOrWhiteSpace(payloadJson)
                        ? $"Received request {request}"
                        : $"Received request {request} with payload {payloadJson}");
            }

            public static void HandledRequest(string request)
            {
                Log.LogInformation((int)EventIds.HandlingRequest, $"Successfully handled request {request}");
            }
        }
    }
}
