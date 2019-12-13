// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class OpenModuleClientAsyncArgs : IOpenClientAsyncArgs
    {
        public readonly TransportType TransportType;
        public readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy;
        public readonly RetryStrategy RetryStrategy;
        public readonly ILogger Logger;

        OpenModuleClientAsyncArgs(
            TransportType transportType,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy,
            RetryStrategy retryStrategy,
            ILogger logger)
        {
            this.TransportType = transportType;
            this.TransientErrorDetectionStrategy = transientErrorDetectionStrategy;
            this.RetryStrategy = retryStrategy;
            this.Logger = logger;
        }
    }

    public class ModuleClientWrapper : IDirectMethodClient
    {
        ModuleClient moduleClient = null;

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
            var info = args as OpenModuleClientAsyncArgs;

            this.moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    info.TransportType,
                    info.TransientErrorDetectionStrategy,
                    info.RetryStrategy,
                    info.Logger);
        }
    }
}
