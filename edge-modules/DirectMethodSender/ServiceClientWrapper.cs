// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class OpenServiceClientAsyncArgs : IOpenClientAsyncArgs
    {
        public readonly string ConnectionString;
        public readonly TransportType TransportType;
        public readonly ILogger Logger;

        OpenServiceClientAsyncArgs(
            string connectionString,
            TransportType transportType,
            ILogger logger)
        {
            this.ConnectionString = connectionString;
            this.TransportType = transportType;
            this.Logger = logger;
        }
    }

    public class ServiceClientWrapper : IDirectMethodClient
    {
        ServiceClient serviceClient = null;
        OpenServiceClientAsyncArgs initInfo = null;
        int DirectMethodCount = 1;

        public async Task CloseClientAsync()
        {
            Preconditions.CheckNotNull(this.serviceClient);
            await this.serviceClient.CloseAsync();
        }

        public async Task InvokeDirectMethodAsync(CancellationTokenSource cts)
        {
            ILogger Logger = this.initInfo.Logger;
            Logger.LogInformation("Invoke DirectMethod from cloud: started.");

            string deviceId = Settings.Current.DeviceId;
            string targetModuleId = Settings.Current.TargetModuleId;
            TimeSpan delay = Settings.Current.DirectMethodDelay;
            CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod("HelloWorldMethod").SetPayloadJson("{ \"Message\": \"Hello\" }");

            while (!cts.Token.IsCancellationRequested)
            {
                Logger.LogInformation($"Calling Direct Method from cloud on device {Settings.Current.DeviceId} targeting module [{Settings.Current.TargetModuleId}] with count {directMethodCount}.");

                try
                {
                    CloudToDeviceMethodResult result = await this.serviceClient.InvokeDeviceMethodAsync(deviceId, targetModuleId, cloudToDeviceMethod, CancellationToken.None);

                    string statusMessage = $"Calling Direct Method from cloud with count {this.DirectMethodCount} returned with status code {result.Status}";
                    if (result.Status == (int)HttpStatusCode.OK)
                    {
                        Logger.LogDebug(statusMessage);
                    }
                    else
                    {
                        Logger.LogError(statusMessage);
                    }

                    // TODO: this needs to happend on the caller
                    // await ReportStatus(targetModuleId, result, analyzerClient);
                    this.DirectMethodCount++;
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception caught with count {this.DirectMethodCount}: {e}");
                }

                await Task.Delay(delay, cts.Token);
            }

            Logger.LogInformation("Invoke DirectMethod from cloud: finished.");

            // TODO: this needs to happend on the caller
            //await serviceClient.CloseAsync();
        }

        public async Task OpenClientAsync(IOpenClientAsyncArgs args)
        {
            if (args == null) throw new ArgumentException();
            var info = args as OpenServiceClientAsyncArgs;

            this.serviceClient = ServiceClient.CreateFromConnectionString(
                info.ConnectionString,
                info.TransportType);

            await serviceClient.OpenAsync();
            this.initInfo = info;
        }
    }
}
