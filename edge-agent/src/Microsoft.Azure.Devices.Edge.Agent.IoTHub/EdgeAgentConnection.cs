// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public class EdgeAgentConnection : IEdgeAgentConnection
    {
        internal const string ExpectedSchemaVersion = "1.0";
        const string PingMethodName = "ping";
        static readonly Task<MethodResponse> PingMethodResponse = Task.FromResult(new MethodResponse(200));
        static readonly TimeSpan DeviceClientInitializationWaitTime = TimeSpan.FromSeconds(5);
        readonly AsyncLock twinLock = new AsyncLock();
        readonly ISerde<DeploymentConfig> desiredPropertiesSerDe;
        readonly Task initTask;
        readonly RetryStrategy retryStrategy;

        Option<IDeviceClient> deviceClient;
        TwinCollection desiredProperties;
        Option<TwinCollection> reportedProperties;
        Option<DeploymentConfigInfo> deploymentConfigInfo;

        static readonly ITransientErrorDetectionStrategy AllButFatalErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.IsFatal() == false);
        static readonly RetryStrategy TransientRetryStrategy =
            new Util.TransientFaultHandling.ExponentialBackoff(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        public EdgeAgentConnection(IDeviceClientProvider deviceClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe)
            : this(deviceClientProvider, desiredPropertiesSerDe, TransientRetryStrategy)
        { }

        internal EdgeAgentConnection(IDeviceClientProvider deviceClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe,
            RetryStrategy retryStrategy)
        {
            this.desiredPropertiesSerDe = Preconditions.CheckNotNull(desiredPropertiesSerDe, nameof(desiredPropertiesSerDe));
            this.deploymentConfigInfo = Option.None<DeploymentConfigInfo>();
            this.reportedProperties = Option.None<TwinCollection>();
            this.deviceClient = Option.None<IDeviceClient>();
            this.retryStrategy = Preconditions.CheckNotNull(retryStrategy, nameof(retryStrategy));
            this.initTask = this.CreateAndInitDeviceClient(Preconditions.CheckNotNull(deviceClientProvider, nameof(deviceClientProvider)));
        }

        async Task CreateAndInitDeviceClient(IDeviceClientProvider deviceClientProvider)
        {
            using (await this.twinLock.LockAsync())
            {
                IDeviceClient dc = await deviceClientProvider.Create(
                    this.OnConnectionStatusChanged,
                    async d =>
                    {
                        await d.SetDesiredPropertyUpdateCallbackAsync(this.OnDesiredPropertiesUpdated);
                        await d.SetMethodHandlerAsync(PingMethodName, this.PingMethodCallback);
                    });
                this.deviceClient = Option.Some(dc);

                await this.RefreshTwinAsync();
            }
        }

        Task<MethodResponse> PingMethodCallback(MethodRequest methodRequest, object userContext) => PingMethodResponse;

        async void OnConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            try
            {
                Events.ConnectionStatusChanged(status, reason);
                if (this.initTask.IsCompleted && status == ConnectionStatus.Connected)
                {
                    using (await this.twinLock.LockAsync())
                    {
                        await this.RefreshTwinAsync();
                    }
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.ConnectionStatusChangedHandlingError(ex);
            }
        }

        async Task OnDesiredPropertiesUpdated(TwinCollection desiredPropertiesPatch, object userContext)
        {
            Events.DesiredPropertiesUpdated();
            using (await this.twinLock.LockAsync())
            {
                if (this.desiredProperties == null || this.desiredProperties.Version + 1 != desiredPropertiesPatch.Version)
                {
                    await this.RefreshTwinAsync();
                }
                else
                {
                    await this.ApplyPatchAsync(desiredPropertiesPatch);
                }
            }
        }

        async Task RefreshTwinAsync()
        {
            try
            {
                // if GetTwinAsync fails its possible that it might be due to transient network errors or because
                // we are getting throttled by IoT Hub; if we didn't attempt a retry then this object would be
                // stuck in an "error" state till either the connection status on the underlying device connection
                // changes or we receive a patch deployment; doing an exponential back-off retry here allows us to
                // recover from this situation
                var retryPolicy = new RetryPolicy(AllButFatalErrorDetectionStrategy, this.retryStrategy);
                retryPolicy.Retrying += (_, args) => Events.RetryingGetTwin(args);
                IDeviceClient dc = this.deviceClient.Expect(() => new InvalidOperationException("DeviceClient not yet initialized"));
                Twin twin = await retryPolicy.ExecuteAsync(() => dc.GetTwinAsync());

                this.desiredProperties = twin.Properties.Desired;
                this.reportedProperties = Option.Some(twin.Properties.Reported);
                await this.UpdateDeploymentConfig();
                Events.TwinRefreshSuccess();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties?.Version ?? 0, ex));
                Events.TwinRefreshError(ex);
            }
        }

        // This method updates local state and should be called only after acquiring twinLock
        async Task ApplyPatchAsync(TwinCollection patch)
        {
            try
            {
                string mergedJson = JsonEx.Merge(this.desiredProperties, patch, true);
                this.desiredProperties = new TwinCollection(mergedJson);
                await this.UpdateDeploymentConfig();
                Events.DesiredPropertiesPatchApplied();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties?.Version ?? 0, ex));
                Events.DesiredPropertiesPatchFailed(ex);
                // Update reported properties with last desired status
            }
        }

        Task UpdateDeploymentConfig()
        {
            DeploymentConfig deploymentConfig;

            try
            {
                // if the twin is empty then throw an appropriate error
                if (this.desiredProperties.Count == 0)
                {
                    throw new ConfigEmptyException("This device has an empty configuration for the edge agent. Please set a deployment manifest.");
                }

                string desiredPropertiesJson = this.desiredProperties.ToJson();
                deploymentConfig = this.desiredPropertiesSerDe.Deserialize(desiredPropertiesJson);
            }
            catch (ConfigEmptyException)
            {
                Events.EmptyDeploymentConfig();
                // TODO: Localize this error?
                throw;
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.ErrorUpdatingDeploymentConfig(ex);
                // TODO: Localize this error?
                throw new ConfigFormatException("Agent configuration format is invalid.", ex);
            }

            try
            {
                // Do any validation on deploymentConfig if necessary
                if (!deploymentConfig.SchemaVersion.Equals(ExpectedSchemaVersion, StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Localize this error?
                    throw new InvalidSchemaVersionException($"Received schema with version {deploymentConfig.SchemaVersion}, but only version {ExpectedSchemaVersion} is supported.");
                }
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties.Version, deploymentConfig));
                Events.UpdatedDeploymentConfig();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.ErrorUpdatingDeploymentConfig(ex);
                throw;
            }

            return Task.CompletedTask;
        }

        public async Task<Option<DeploymentConfigInfo>> GetDeploymentConfigInfoAsync()
        {
            await this.WaitForDeviceClientInitialization();
            return this.deploymentConfigInfo;
        }

        async Task<bool> WaitForDeviceClientInitialization() =>
            await Task.WhenAny(this.initTask, Task.Delay(DeviceClientInitializationWaitTime)) == this.initTask;

        public Option<TwinCollection> ReportedProperties => this.reportedProperties;

        public void Dispose() => this.deviceClient.ForEach(d => d.Dispose());

        public async Task UpdateReportedPropertiesAsync(TwinCollection patch)
        {
            if (await this.WaitForDeviceClientInitialization())
            {
                await this.deviceClient.ForEachAsync(d => d.UpdateReportedPropertiesAsync(patch));
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeAgentConnection>();
            const int IdStart = AgentEventIds.EdgeAgentConnection;

            enum EventIds
            {
                DesiredPropertiesFailed = IdStart,
                ConnectionStatusChanged,
                DesiredPropertiesPatchApplied,
                DesiredPropertiesUpdated,
                DeploymentConfigUpdated,
                ErrorUpdatingDeploymentConfig,
                ErrorRefreshingTwin,
                TwinRefreshSuccess,
                ErrorHandlingConnectionChangeEvent,
                EmptyDeploymentConfig,
                RetryingGetTwin
            }

            public static void DesiredPropertiesPatchFailed(Exception exception)
            {
                Log.LogError((int)EventIds.DesiredPropertiesFailed, exception, "Edge agent failed to process desired properties update patch");
            }

            public static void ConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Log.LogDebug((int)EventIds.ConnectionStatusChanged, $"Connection status changed to {status} with reason {reason}");
            }

            internal static void DesiredPropertiesUpdated()
            {
                Log.LogDebug((int)EventIds.DesiredPropertiesUpdated, "Edge agent desired properties updated callback invoked.");
            }

            internal static void DesiredPropertiesPatchApplied()
            {
                Log.LogDebug((int)EventIds.DesiredPropertiesPatchApplied, "Edge agent desired properties patch applied successfully.");
            }

            internal static void ConnectionStatusChangedHandlingError(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingConnectionChangeEvent, ex, "Edge agent connection error handing connection change callback.");
            }

            internal static void TwinRefreshSuccess()
            {
                Log.LogDebug((int)EventIds.TwinRefreshSuccess, "Updated edge agent configuration from twin.");
            }

            internal static void TwinRefreshError(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorRefreshingTwin, ex, "Error refreshing edge agent configuration from twin.");
            }

            internal static void ErrorUpdatingDeploymentConfig(Exception ex)
            {
                Log.LogError((int)EventIds.ErrorUpdatingDeploymentConfig, ex, "Error updating deployment config from edge agent desired properties.");
            }

            internal static void EmptyDeploymentConfig()
            {
                Log.LogInformation((int)EventIds.EmptyDeploymentConfig, "Deployment config in edge agent's desired properties is empty.");
            }

            internal static void UpdatedDeploymentConfig()
            {
                Log.LogDebug((int)EventIds.DeploymentConfigUpdated, "Edge agent updated deployment config from desired properties.");
            }

            internal static void RetryingGetTwin(RetryingEventArgs args)
            {
                Log.LogDebug((int)EventIds.RetryingGetTwin, $"Edge agent is retrying GetTwinAsync. Attempt #{args.CurrentRetryCount}. Last error: {args.LastException?.Message}");
            }
        }
    }
}
