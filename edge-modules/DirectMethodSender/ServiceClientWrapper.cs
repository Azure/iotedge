// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    public class OpenServiceClientAsyncArgs : IOpenClientAsyncArgs
    {
        public readonly string ConnectionString;
        public readonly TransportType TransportType;

        OpenServiceClientAsyncArgs(
            string connectionString,
            TransportType transportType)
        {
            this.ConnectionString = connectionString;
            this.TransportType = transportType;
        }
    }

    public class ServiceClientWrapper : IDirectMethodClient
    {
        ServiceClient serviceClient = null;

        public Task CloseClientAsync()
        {
            throw new System.NotImplementedException();
        }

        public int InvokeDirectMethodAsync()
        {
            throw new System.NotImplementedException();
        }

        public async Task OpenClientAsync(IOpenClientAsyncArgs args)
        {
            if (args == null) throw new ArgumentException();
            var info = args as OpenServiceClientAsyncArgs;

            this.serviceClient = ServiceClient.CreateFromConnectionString(
                info.ConnectionString,
                info.TransportType);

            await serviceClient.OpenAsync();
        }
    }
}
