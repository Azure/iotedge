// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    interface IRequestHandler
    {
        Task<Option<string>> HandleRequest(Option<string> payloadJson);
    }
}
