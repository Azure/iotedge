// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;

    public interface IRequestManager
    {
        Task<(int, string)> ProcessRequest(string request, string payloadJson);
    }
}
