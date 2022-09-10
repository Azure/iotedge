using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{

    public class ModuleClientWrapper : IDisposable
    {
        Option<ModuleClient> inner;
        SemaphoreSlim moduleClientLock;
        UploadTarget uploadTarget;
        CancellationTokenSource cancellationTokenSource;

        public ModuleClientWrapper(Option<ModuleClient> moduleClient, SemaphoreSlim moduleClientLock, UploadTarget uploadTarget, CancellationTokenSource cts)
        {
            this.inner = moduleClient;
            this.moduleClientLock = Preconditions.CheckNotNull(moduleClientLock);
            this.uploadTarget = uploadTarget;
            this.cancellationTokenSource = cts;
        }

        public static async Task<ModuleClientWrapper> BuildModuleClientWrapperAsync(UploadTarget uploadTarget, CancellationTokenSource cts)
        {
            SemaphoreSlim moduleClientLock = new SemaphoreSlim(1, 1);
            Option<ModuleClient> moduleClient = await InitializeModuleClientAsync();
            return new ModuleClientWrapper(moduleClient, moduleClientLock, uploadTarget, cts);
        }

        public async Task RecreateClientAsync()
        {
            await this.moduleClientLock.WaitAsync();

            try
            {
                this.inner.ForEach((client) =>
                {
                    client.Dispose();
                });
                this.inner = await InitializeModuleClientAsync();
                this.inner.ForEach((client) =>
                {
                    LoggerUtil.Writer.LogInformation("Closed and re-established connection to IoT Hub");
                });
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError($"Failed closing and re-establishing connection to IoT Hub: {e.ToString()}");
                this.cancellationTokenSource.Cancel();
            }

            this.moduleClientLock.Release();
        }

        // This method is only called in the IotMessage upload target code path.
        // The inner ModuleClient in this case must exist (i.e. Option is Some).
        // This is enforced by below RecreateClientAsync by exiting application if
        // upload target is IoTMessage and cannot initialize client.
        public async Task SendMessage(string outputName, Message message)
        {
            await this.moduleClientLock.WaitAsync();

            await this.inner.Match(async (client) =>
            {
                try
                {
                    await client.SendEventAsync(outputName, message);
                    LoggerUtil.Writer.LogInformation("Successfully sent metrics via IoT message");
                }
                catch (Exception e)
                {
                    LoggerUtil.Writer.LogError($"Failed sending metrics as IoT message: {e.ToString()}");
                }
            }, () =>
            {
                LoggerUtil.Writer.LogError($"Client unexpectedly not initialized. Cannot send message.");
                return Task.CompletedTask;
            });

            this.moduleClientLock.Release();
        }

        public void Dispose()
        {
            this.inner.ForEach((client) =>
            {
                client.Dispose();
            });
            this.moduleClientLock.Dispose();
        }

        // Throws if cannot initialize client for IotMessage upload target.
        static async Task<Option<ModuleClient>> InitializeModuleClientAsync()
        {
            TransportType transportType = TransportType.Amqp_Tcp_Only;
            LoggerUtil.Writer.LogInformation($"Trying to initialize module client using transport type [{transportType}]");

            try
            {
                ITransportSettings[] settings = new ITransportSettings[] { new AmqpTransportSettings(transportType) };
                ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                moduleClient.ProductInfo = Constants.ProductInfo;

                await moduleClient.OpenAsync();
                LoggerUtil.Writer.LogInformation($"Successfully initialized module client using transport type [{transportType}]");
                return Option.Some(moduleClient);
            }
            catch (Exception e)
            {
                // This should not block or be a problem for Azure Monitor upload path.
                if (Settings.Current.UploadTarget == UploadTarget.IotMessage)
                {
                    LoggerUtil.Writer.LogError("Error connecting to Edge Hub. Exception: {0}", e);
                    throw;
                }

                return Option.None<ModuleClient>();
            }
        }
    }
}
