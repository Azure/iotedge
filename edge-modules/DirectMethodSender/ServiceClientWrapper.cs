// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class OpenServiceClientAsyncArgs : IOpenClientAsyncArgs
    {
            public string connectionString;
            public TransportType transportType;
    }

    public class ServiceClientWrapper : IDirectMethodClient
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
