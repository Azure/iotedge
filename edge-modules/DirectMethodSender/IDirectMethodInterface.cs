// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDirectMethodClient
    {
        Task CloseClientAsync();

        // Invoke direct method using a client
        Task<HttpStatusCode> InvokeDirectMethodAsync(CancellationTokenSource cts);

        // Create a client and open an instance
        Task OpenClientAsync(IOpenClientAsyncArgs args);
    }

    public interface IOpenClientAsyncArgs
    {
        // Intentionally left blank here to be implemented in the class that implement the IDirectMethodClient methods
    }
}