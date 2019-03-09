// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    public interface IStreamRequestListener
    {
        void InitPump(IModuleClient moduleClient);
    }
}
