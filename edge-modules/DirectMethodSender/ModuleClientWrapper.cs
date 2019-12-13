// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
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
        OpenModuleClientAsyncArgs initInfo = null;
        int DirectMethodCount = 1;

        public async Task CloseClientAsync()
        {
            Preconditions.CheckNotNull(this.moduleClient);
            await this.moduleClient.CloseAsync();
        }

        public async Task InvokeDirectMethodAsync(CancellationTokenSource cts)
        {
            ILogger Logger = this.initInfo.Logger;
            Logger.LogInformation("Invoke DirectMethod from module: started.");

            string deviceId = Settings.Current.DeviceId;
            string targetModuleId = Settings.Current.TargetModuleId;
            TimeSpan delay = Settings.Current.DirectMethodDelay;
            var request = new MethodRequest("HelloWorldMethod", Encoding.UTF8.GetBytes("{ \"Message\": \"Hello\" }"));

            while (!cts.Token.IsCancellationRequested)
            {
                Logger.LogInformation($"Calling Direct Method on device {deviceId} targeting module {targetModuleId}.");

                try
                {
                    MethodResponse response = await moduleClient.InvokeMethodAsync(deviceId, targetModuleId, request);

                    string statusMessage = $"Calling Direct Method from module with count {this.DirectMethodCount} returned with status code {response.Status}";
                    if (response.Status == (int)HttpStatusCode.OK)
                    {
                        Logger.LogDebug(statusMessage);
                    }
                    else
                    {
                        Logger.LogError(statusMessage);
                    }

                    // TODO: this needs to be on the caller
                    //await ReportStatus(targetModuleId, response, analyzerClient);

                    this.DirectMethodCount++;
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception caught with count {this.DirectMethodCount}: {e}");
                }

                await Task.Delay(delay, cts.Token);
            }

            Logger.LogInformation("Invoke DirectMethod from module: finished.");
        }

        public async Task OpenClientAsync(IOpenClientAsyncArgs args)
        {
            if (args == null) throw new ArgumentException();
            var info = args as OpenModuleClientAsyncArgs;

            // implicit OpenAsync()
            this.moduleClient = await ModuleUtil.CreateModuleClientAsync(
                    info.TransportType,
                    info.TransientErrorDetectionStrategy,
                    info.RetryStrategy,
                    info.Logger);
            this.initInfo = info;
        }
    }
}
