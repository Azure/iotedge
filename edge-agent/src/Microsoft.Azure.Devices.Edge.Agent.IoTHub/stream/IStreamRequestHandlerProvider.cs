// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    public interface IStreamRequestHandlerProvider
    {
        bool TryGetHandler(string requestName, out IStreamRequestHandler handler);
    }
}
