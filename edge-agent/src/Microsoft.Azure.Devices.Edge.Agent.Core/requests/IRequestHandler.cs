// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;

    interface IRequestHandler
    {
        Task<string> HandleRequest(string payloadJson);
    }
}
