// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using System.Net;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            switch (context.Exception)
            {
                case ArgumentException argEx:
                    Events.ArgumentException(context);
                    context.Result = new ObjectResult(argEx.Message)
                    {
                        StatusCode = (int)HttpStatusCode.BadRequest
                    };
                    break;

                default:
                    Events.UnknownException(context);
                    context.Result = new ObjectResult(context.Exception)
                    {
                        StatusCode = (int)HttpStatusCode.InternalServerError
                    };
                    break;
            }
        }

        static class Events
        {
            const int IdStart = HttpEventIds.ExceptionFilter;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ExceptionFilter>();

            enum EventIds
            {
                ArgumentException = IdStart,
                UnknownException
            }

            public static void ArgumentException(ExceptionContext context)
            {
                Log.LogInformation((int)EventIds.ArgumentException, $"Exception filter got ArgumentException - {context.Exception}");
            }

            public static void UnknownException(ExceptionContext context)
            {
                Log.LogInformation((int)EventIds.UnknownException, $"Exception filter got unknown exception - {context.Exception}");
            }
        }
    }
}
