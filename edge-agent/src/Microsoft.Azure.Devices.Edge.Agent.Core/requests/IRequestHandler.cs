// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IRequestHandler
    {
        string RequestName { get; }

        Task<Option<string>> HandleRequest(Option<string> payloadJson);
    }
}
