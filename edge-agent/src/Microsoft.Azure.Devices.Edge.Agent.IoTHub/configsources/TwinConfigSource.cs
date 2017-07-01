// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class TwinConfigSource : BaseConfigSource
    {
        ISerde<ModuleSet> ModuleSetSerde { get; }

        ISerde<Diff> DiffSerde { get; }

        readonly IDeviceClient deviceClient;

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
                Events.DesiredPropertiesFailed(ex);
                this.OnFailed(ex);
                return Task.FromException(ex);
            }
        }

        TwinConfigSource(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, IConfiguration configuration)
            : base(configuration)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.ModuleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
            this.DiffSerde = Preconditions.CheckNotNull(diffSerde, nameof(diffSerde));
            Events.Created();
        }

        public override void Dispose()
        {
            this.deviceClient.Dispose();
        }

        public override async Task<ModuleSet> GetModuleSetAsync()
        {
            Twin twin = await this.deviceClient.GetTwinAsync();

            try
            {
                return this.ModuleSetSerde.Deserialize(twin.Properties.Desired.ToJson());
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                this.OnFailed(ex);
                throw;
            }
        }

        public override event EventHandler<Diff> ModuleSetChanged;

        protected void OnChanged(Diff diff)
        {
            this.ModuleSetChanged?.Invoke(this, diff);
        }

        public override event EventHandler<Exception> ModuleSetFailed;

        protected void OnFailed(Exception ex)
        {
            this.ModuleSetFailed?.Invoke(this, ex);
        }

        public static async Task<TwinConfigSource> Create(IDeviceClient deviceClient, ISerde<ModuleSet> moduleSetSerde, ISerde<Diff> diffSerde, IConfiguration configuration)
        {
            var configSource = new TwinConfigSource(deviceClient, moduleSetSerde, diffSerde, configuration);
            await configSource.deviceClient.SetDesiredPropertyUpdateCallback(configSource.OnDesiredPropertyChanged, null);
            return configSource;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();
            const int IdStart = AgentEventIds.TwinConfigSource;

            enum EventIds
            {
                Created = IdStart,
                DesiredPropertiesFailed,
            }

            public static void Created()
            {
                Log.LogDebug((int)EventIds.Created, "TwinConfigSource Created");
            }

            public static void DesiredPropertiesFailed(Exception exception)
            {
                Log.LogError((int)EventIds.DesiredPropertiesFailed, exception, "TwinConfigSource failed processing desired configuration ");
            }
        }
    }
}