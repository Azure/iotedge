// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IRequestManager
    {
        Task<(int statusCode, Option<string> responsePayload)> ProcessRequest(string request, string payloadJson);
    }
}
