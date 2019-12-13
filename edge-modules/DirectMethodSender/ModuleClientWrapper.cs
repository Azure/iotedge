// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class OpenModuleClientAsyncArgs : IOpenClientAsyncArgs
    {
            public TransportType transportType;
            public ITransientErrorDetectionStrategy transientErrorDetectionStrategy;
            public RetryStrategy retryStrategy;
            public ILogger logger;
    }

    public class ModuleClientWrapper : IDirectMethodClient
    {
        public Task CloseClientAsync()
        {
            throw new System.NotImplementedException();
        }

        public int InvokeDirectMethodAsync()
        {
            throw new System.NotImplementedException();
        }

        public Task OpenClientAsync(IOpenClientAsyncArgs args)
        {
            throw new System.NotImplementedException();
        }
    }
}
