// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    public class EdgeAgentConnection : IEdgeAgentConnection
    {
        internal static readonly Version ExpectedSchemaVersion = new Version("1.0");
        static readonly TimeSpan DefaultConfigRefreshFrequency = TimeSpan.FromHours(1);
        static readonly TimeSpan DeviceClientInitializationWaitTime = TimeSpan.FromSeconds(5);

        static readonly ITransientErrorDetectionStrategy AllButFatalErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.IsFatal() == false);

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        readonly AsyncLock twinLock = new AsyncLock();
        readonly ISerde<DeploymentConfig> desiredPropertiesSerDe;
        readonly Task initTask;
        readonly RetryStrategy retryStrategy;
        readonly PeriodicTask refreshTwinTask;
        readonly IModuleConnection moduleConnection;
        readonly bool pullOnReconnect;
        readonly IDeviceManager deviceManager;

        Option<TwinCollection> desiredProperties;
        Option<TwinCollection> reportedProperties;
        Option<DeploymentConfigInfo> deploymentConfigInfo;

        public EdgeAgentConnection(
            IModuleClientProvider moduleClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe,
            IRequestManager requestManager,
            IDeviceManager deviceManager)
            : this(moduleClientProvider, desiredPropertiesSerDe, requestManager, deviceManager, true, DefaultConfigRefreshFrequency, TransientRetryStrategy)
        {
        }

        public EdgeAgentConnection(
            IModuleClientProvider moduleClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe,
            IRequestManager requestManager,
            IDeviceManager deviceManager,
            bool enableSubscriptions,
            TimeSpan configRefreshFrequency)
            : this(moduleClientProvider, desiredPropertiesSerDe, requestManager, deviceManager, enableSubscriptions, configRefreshFrequency, TransientRetryStrategy)
        {
        }

        internal EdgeAgentConnection(
            IModuleClientProvider moduleClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe,
            IRequestManager requestManager,
            IDeviceManager deviceManager,
            bool enableSubscriptions,
            TimeSpan refreshConfigFrequency,
            RetryStrategy retryStrategy)
        {
            this.desiredPropertiesSerDe = Preconditions.CheckNotNull(desiredPropertiesSerDe, nameof(desiredPropertiesSerDe));
            this.deploymentConfigInfo = Option.None<DeploymentConfigInfo>();
            this.reportedProperties = Option.None<TwinCollection>();
            this.moduleConnection = new ModuleConnection(moduleClientProvider, requestManager, this.OnConnectionStatusChanged, this.OnDesiredPropertiesUpdated, enableSubscriptions);
            this.retryStrategy = Preconditions.CheckNotNull(retryStrategy, nameof(retryStrategy));
            this.refreshTwinTask = new PeriodicTask(this.ForceRefreshTwin, refreshConfigFrequency, refreshConfigFrequency, Events.Log, "refresh twin config");
            this.initTask = this.ForceRefreshTwin();
            this.pullOnReconnect = enableSubscriptions;
            this.deviceManager = Preconditions.CheckNotNull(deviceManager, nameof(deviceManager));
            Events.TwinRefreshInit(refreshConfigFrequency);
        }

        public Option<TwinCollection> ReportedProperties => this.reportedProperties;

        public IModuleConnection ModuleConnection => this.moduleConnection;

        public async Task<Option<DeploymentConfigInfo>> GetDeploymentConfigInfoAsync()
        {
            await this.WaitForDeviceClientInitialization();
            return this.deploymentConfigInfo;
        }

        public void Dispose()
        {
            this.refreshTwinTask.Dispose();
            this.moduleConnection.Dispose();
        }

        public async Task UpdateReportedPropertiesAsync(TwinCollection patch)
        {
            Events.UpdatingReportedProperties();
            try
            {
                Option<IModuleClient> moduleClient = this.moduleConnection.GetModuleClient();
                if (!moduleClient.HasValue)
                {
                    Events.UpdateReportedPropertiesDeviceClientEmpty();
                    return;
                }

                await moduleClient.ForEachAsync(d => d.UpdateReportedPropertiesAsync(patch));
                Events.UpdatedReportedProperties();
            }
            catch (Exception e)
            {
                Events.ErrorUpdatingReportedProperties(e);
                throw;
            }
        }

        public async Task SendEventAsync(Message message)
        {
            Events.UpdatingReportedProperties();
            try
            {
                Option<IModuleClient> moduleClient = this.moduleConnection.GetModuleClient();
                if (!moduleClient.HasValue)
                {
                    Events.SendEventClientEmpty();
                    return;
                }

                await moduleClient.ForEachAsync(d => d.SendEventAsync(message));
                Events.SendEvent();
            }
            catch (Exception e)
            {
                Events.ErrorSendingEvent(e);
                throw;
            }
        }

        internal static void ValidateSchemaVersion(string schemaVersion)
        {
            if (ExpectedSchemaVersion.CompareMajorVersion(schemaVersion, "desired properties schema") != 0)
            {
                Events.MismatchedMinorVersions(schemaVersion, ExpectedSchemaVersion);
            }
        }

        async Task ForceRefreshTwin()
        {
            using (await this.twinLock.LockAsync())
            {
                await this.RefreshTwinAsync();
            }
        }

        async void OnConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            try
            {
                UpstreamProtocol protocol =
                    this.ModuleConnection.GetModuleClient().Map(x => x.UpstreamProtocol).GetOrElse(UpstreamProtocol.Amqp);
                Events.ConnectionStatusChanged(status, reason);

                // Notify the IoT Edge daemon that a device has been deprovisioned and it should check
                // if the device has been provisioned to a different IoT hub instead.
                // When the Amqp or AmqpWs protocol is used, the SDK returns a connection status change reason of
                // Device_Disabled when a device is either disabled or deleted in IoT hub.
                // For the Mqtt and MqttWs protocol however, the SDK returns a Bad_Credential status as it's not
                // possible for IoT hub to distinguish between 'device does not exist', 'device is disabled' and
                // 'device exists but wrong credentials were supplied' cases.
                if ((reason == ConnectionStatusChangeReason.Device_Disabled &&
                    (protocol == UpstreamProtocol.Amqp || protocol == UpstreamProtocol.AmqpWs)) ||
                    (reason == ConnectionStatusChangeReason.Bad_Credential &&
                    (protocol == UpstreamProtocol.Mqtt || protocol == UpstreamProtocol.MqttWs)))
                {
                    await this.deviceManager.ReprovisionDeviceAsync();
                }

                if (this.pullOnReconnect && this.initTask.IsCompleted && status == ConnectionStatus.Connected)
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
                await this.desiredProperties
                    .Filter(d => d.Version + 1 == desiredPropertiesPatch.Version)
                    .Map(d => this.ApplyPatchAsync(d, desiredPropertiesPatch))
                    .GetOrElse(this.RefreshTwinAsync);
            }
        }

        // This method updates local state and should be called only after acquiring twinLock
        async Task RefreshTwinAsync()
        {
            Events.TwinRefreshStart();
            Option<Twin> twinOption = await this.GetTwinFromIoTHub();

            await twinOption.ForEachAsync(
                async twin =>
                {
                    try
                    {
                        this.desiredProperties = Option.Some(twin.Properties.Desired);
                        this.reportedProperties = Option.Some(twin.Properties.Reported);
                        await this.UpdateDeploymentConfig(twin.Properties.Desired);
                        Events.TwinRefreshSuccess();
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties.Map(d => d.Version).GetOrElse(0), ex));
                        Events.TwinRefreshError(ex);
                    }
                });
        }

        async Task<Option<Twin>> GetTwinFromIoTHub()
        {
            try
            {
                async Task<Twin> GetTwinFunc()
                {
                    Events.GettingModuleClient();
                    IModuleClient moduleClient = await this.moduleConnection.GetOrCreateModuleClient();
                    Events.GotModuleClient();
                    return await moduleClient.GetTwinAsync();
                }

                // if GetTwinAsync fails its possible that it might be due to transient network errors or because
                // we are getting throttled by IoT Hub; if we didn't attempt a retry then this object would be
                // stuck in an "error" state till either the connection status on the underlying device connection
                // changes or we receive a patch deployment; doing an exponential back-off retry here allows us to
                // recover from this situation
                var retryPolicy = new RetryPolicy(AllButFatalErrorDetectionStrategy, this.retryStrategy);
                retryPolicy.Retrying += (_, args) => Events.RetryingGetTwin(args);
                Twin twin = await retryPolicy.ExecuteAsync(GetTwinFunc);
                Events.GotTwin(twin);
                return Option.Some(twin);
            }
            catch (Exception e)
            {
                Events.ErrorGettingTwin(e);
                return Option.None<Twin>();
            }
        }

        // This method updates local state and should be called only after acquiring twinLock
        async Task ApplyPatchAsync(TwinCollection desiredProperties, TwinCollection patch)
        {
            try
            {
                string mergedJson = JsonEx.Merge(desiredProperties, patch, true);
                desiredProperties = new TwinCollection(mergedJson);
                this.desiredProperties = Option.Some(desiredProperties);
                await this.UpdateDeploymentConfig(desiredProperties);
                Events.DesiredPropertiesPatchApplied();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(desiredProperties?.Version ?? 0, ex));
                Events.DesiredPropertiesPatchFailed(ex);
                // Update reported properties with last desired status
            }
        }

        Task UpdateDeploymentConfig(TwinCollection desiredProperties)
        {
            DeploymentConfig deploymentConfig;

            try
            {
                // if the twin is empty then throw an appropriate error
                if (desiredProperties.Count == 0)
                {
                    throw new ConfigEmptyException("This device has an empty configuration for the edge agent. Please set a deployment manifest.");
                }

                string desiredPropertiesJson = desiredProperties.ToJson();
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
                ValidateSchemaVersion(deploymentConfig.SchemaVersion);
                this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(desiredProperties.Version, deploymentConfig));
                Events.UpdatedDeploymentConfig();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.ErrorUpdatingDeploymentConfig(ex);
                throw;
            }

            return Task.CompletedTask;
        }

        async Task<bool> WaitForDeviceClientInitialization() =>
            await Task.WhenAny(this.initTask, Task.Delay(DeviceClientInitializationWaitTime)) == this.initTask;

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeAgentConnection>();
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
                RetryingGetTwin,
                MismatchedSchemaVersion,
                TwinRefreshInit,
                TwinRefreshStart,
                GotTwin,
                UpdatingReportedProperties,
                UpdateReportedPropertiesDeviceClientEmpty,
                UpdatedReportedProperties,
                ErrorUpdatingReportedProperties,
                GotModuleClient,
                GettingModuleClient,
                SendEvent,
                SendEventClientEmpty,
                ErrorSendingEvent,
            }

            public static void DesiredPropertiesPatchFailed(Exception exception)
            {
                Log.LogError((int)EventIds.DesiredPropertiesFailed, exception, "Edge agent failed to process desired properties update patch");
            }

            public static void ConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Log.LogDebug((int)EventIds.ConnectionStatusChanged, $"Connection status changed to {status} with reason {reason}");
            }

            public static void MismatchedMinorVersions(string receivedVersion, Version expectedVersion)
            {
                Log.LogWarning(
                    (int)EventIds.MismatchedSchemaVersion,
                    $"Desired properties schema version {receivedVersion} does not match expected schema version {expectedVersion}. Some settings may not be supported.");
            }

            public static void GotTwin(Twin twin)
            {
                long reportedPropertiesVersion = twin?.Properties?.Reported?.Version ?? -1;
                long desiredPropertiesVersion = twin?.Properties?.Desired?.Version ?? -1;
                Log.LogInformation((int)EventIds.GotTwin, $"Obtained Edge agent twin from IoTHub with desired properties version {desiredPropertiesVersion} and reported properties version {reportedPropertiesVersion}.");
            }

            public static void UpdatingReportedProperties()
            {
                Log.LogDebug((int)EventIds.UpdatingReportedProperties, "Updating reported properties in IoT Hub");
            }

            public static void UpdateReportedPropertiesDeviceClientEmpty()
            {
                Log.LogDebug((int)EventIds.UpdateReportedPropertiesDeviceClientEmpty, "Updating reported properties in IoT Hub");
            }

            public static void UpdatedReportedProperties()
            {
                Log.LogDebug((int)EventIds.UpdatedReportedProperties, "Updated reported properties in IoT Hub");
            }

            public static void ErrorUpdatingReportedProperties(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorUpdatingReportedProperties, ex, "Error updating reported properties in IoT Hub");
            }

            public static void GettingModuleClient()
            {
                Log.LogDebug((int)EventIds.GettingModuleClient, "Getting module client to refresh the twin");
            }

            public static void GotModuleClient()
            {
                Log.LogDebug((int)EventIds.GotModuleClient, "Got module client to refresh the twin");
            }

            public static void ErrorGettingTwin(Exception e)
            {
                Log.LogWarning((int)EventIds.RetryingGetTwin, e, "Error getting edge agent twin from IoTHub");
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

            internal static void TwinRefreshInit(TimeSpan interval)
            {
                Log.LogDebug((int)EventIds.TwinRefreshInit, "Initialize twin refresh with interval '{0:c}'", interval);
            }

            internal static void TwinRefreshStart()
            {
                Log.LogDebug((int)EventIds.TwinRefreshStart, "Begin refreshing twin from upstream...");
            }

            internal static void TwinRefreshSuccess()
            {
                Log.LogDebug((int)EventIds.TwinRefreshSuccess, "Updated edge agent configuration from upstream twin.");
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

            internal static void SendEvent()
            {
                Log.LogDebug((int)EventIds.SendEvent, $"Edge agent is sending a diagnostic message.");
            }

            public static void SendEventClientEmpty()
            {
                Log.LogDebug((int)EventIds.SendEventClientEmpty, "Client empty.");
            }

            public static void ErrorSendingEvent(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorSendingEvent, ex, "Error sending event");
            }
        }
    }
}
