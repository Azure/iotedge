// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public class TwinConfigSource : IConfigSource
    {
        ISerde<ModuleSet> ModuleSetSerde { get; }

        ISerde<Diff> DiffSerde { get; }

        readonly IDeviceClient deviceClient;

        readonly ILogger logger;

        Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Diff diff = this.DiffSerde.Deserialize(desiredProperties.ToJson());
                this.OnChanged(diff);
                return Task.CompletedTask;
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                this.logger.LogError(0, ex, $"Error getting twin desired property.");
                this.OnFailed(ex);
                return Task.FromException(ex);
            }
            
        }

        TwinConfigSource(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, ILoggerFactory loggingFactory)
        {
            this.logger = Preconditions.CheckNotNull(loggingFactory, nameof(loggingFactory))
                .CreateLogger<TwinConfigSource>();
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.ModuleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
            this.DiffSerde = Preconditions.CheckNotNull(diffSerde, nameof(diffSerde));
        }

        public void Dispose()
        {
            this.deviceClient.Dispose();
        }

        public async Task<ModuleSet> GetConfigAsync()
        {
            Twin twin = await this.deviceClient.GetTwinAsync();

            try
            {
                return this.ModuleSetSerde.Deserialize(twin.Properties.Desired.ToJson());
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                this.logger.LogError(0, ex, $"Error getting MMA twin configuration.");
                this.OnFailed(ex);
                throw;
            }
        }

        public event EventHandler<Diff> Changed;

        protected void OnChanged(Diff diff)
        {
            this.Changed?.Invoke(this, diff);
        }

        public event EventHandler<Exception> Failed;

        protected void OnFailed(Exception ex)
        {
            this.Failed?.Invoke(this, ex);
        }

        public static async Task<TwinConfigSource> Create(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, ILoggerFactory loggingFactory)
        {
            var configSource = new TwinConfigSource(deviceClient, moduleSetSerde, diffSerde, loggingFactory);
            await configSource.deviceClient.SetDesiredPropertyUpdateCallback(configSource.OnDesiredPropertyChanged, null);
            return configSource;
        }
    }
}