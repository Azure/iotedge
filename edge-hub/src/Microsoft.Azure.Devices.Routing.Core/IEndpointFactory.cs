// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public interface IEndpointFactory
    {
        Endpoint CreateSystemEndpoint(string systemEndpoint);

        Endpoint CreateFunctionEndpoint(string function, string parameterString);
    }
}