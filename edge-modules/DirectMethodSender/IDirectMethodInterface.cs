// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System.Threading.Tasks;

    public interface IDirectMethodClient
    {
        // BEARWASHERE -- TODO
        // BAsically returns nothing
        Task CloseClientAsync();

        // Ret: either MethodResponse.Status or CloudToDeviceMethodResult.Status both are int
        int InvokeDirectMethodAsync();

        Task OpenClientAsync(IOpenClientAsyncArgs args);
    }

    public interface IOpenClientAsyncArgs
    {
        // Intentionally left blank here to be implemented in the class that implement the IDirectMethodClient methods
    }
}