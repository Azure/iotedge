// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodReceiver
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    class DirectMethodReceiver : IDisposable
    {
        private IConfiguration configuration;
        long directMethodCount = 1;
        ILogger logger;
        ModuleClient moduleClient;

        public DirectMethodReceiver(
            ILogger logger,
            IConfiguration configuration,
            ModuleClient moduleClient)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.moduleClient = moduleClient;
        }

        public static async Task<DirectMethodReceiver> CreateAsync(
            ILogger logger,
            IConfiguration configuration)
        {
            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only),
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                logger);

            return new DirectMethodReceiver(
                logger,
                configuration,
                moduleClient);
        }

        public void Dispose() => this.moduleClient?.Dispose();

        Task<MethodResponse> HelloWorldMethodAsync(MethodRequest methodRequest, object userContext)
        {
            this.logger.LogInformation("Received direct method call.");
            // Send the report here
            // Increment the number
            this.directMethodCount++;
            return Task.FromResult(new MethodResponse((int)HttpStatusCode.OK));
        }

        public async Task StartAsync()
        {
            await this.moduleClient.OpenAsync();
            await this.moduleClient.SetMethodHandlerAsync("HelloWorldMethod", this.HelloWorldMethodAsync, null);
        }
    }
}