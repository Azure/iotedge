// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.DeviceManager;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    public class EdgeAgentConnection : IEdgeAgentConnection
    {
        const int PullFrequencyThreshold = 10;

        internal static readonly Version ExpectedSchemaVersion = new Version("1.1.0");
        static readonly TimeSpan DefaultConfigRefreshFrequency = TimeSpan.FromHours(1);
        static readonly TimeSpan DeviceClientInitializationWaitTime = TimeSpan.FromSeconds(5);
        static readonly TimeSpan DefaultTwinPullOnConnectThrottleTime = TimeSpan.FromSeconds(30);

        static readonly ITransientErrorDetectionStrategy AllButFatalErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.IsFatal() == false);

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        readonly AsyncLock twinLock = new AsyncLock();
        readonly ISerde<DeploymentConfig> desiredPropertiesSerDe;
        readonly Task initTask;
        readonly RetryStrategy retryStrategy;
        readonly PeriodicTask refreshTwinTask;
        readonly IModuleConnection moduleConnection;
        readonly bool pullOnReconnect;
        readonly IDeviceManager deviceManager;
        readonly IDeploymentMetrics deploymentMetrics;
        readonly Option<X509Certificate2> manifestTrustBundle;
        readonly TimeSpan twinPullOnConnectThrottleTime;

        Option<TwinCollection> desiredProperties;
        Option<TwinCollection> reportedProperties;
        Option<DeploymentConfigInfo> deploymentConfigInfo;

        DateTime lastTwinPullOnConnect = DateTime.MinValue;
        AtomicBoolean isDelayedTwinPullInProgress = new AtomicBoolean(false);
        int pullRequestCounter = 0;

        public EdgeAgentConnection(
            IModuleClientProvider moduleClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe,
            IRequestManager requestManager,
            IDeviceManager deviceManager,
            IDeploymentMetrics deploymentMetrics,
            Option<X509Certificate2> manifestTrustBundle)
            : this(moduleClientProvider, desiredPropertiesSerDe, requestManager, deviceManager, true, DefaultConfigRefreshFrequency, TransientRetryStrategy, deploymentMetrics, manifestTrustBundle, DefaultTwinPullOnConnectThrottleTime)
        {
        }

        public EdgeAgentConnection(
            IModuleClientProvider moduleClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe,
            IRequestManager requestManager,
            IDeviceManager deviceManager,
            bool enableSubscriptions,
            TimeSpan configRefreshFrequency,
            IDeploymentMetrics deploymentMetrics,
            Option<X509Certificate2> manifestTrustBundle)
            : this(moduleClientProvider, desiredPropertiesSerDe, requestManager, deviceManager, enableSubscriptions, configRefreshFrequency, TransientRetryStrategy, deploymentMetrics, manifestTrustBundle, DefaultTwinPullOnConnectThrottleTime)
        {
        }

        internal EdgeAgentConnection(
            IModuleClientProvider moduleClientProvider,
            ISerde<DeploymentConfig> desiredPropertiesSerDe,
            IRequestManager requestManager,
            IDeviceManager deviceManager,
            bool enableSubscriptions,
            TimeSpan refreshConfigFrequency,
            RetryStrategy retryStrategy,
            IDeploymentMetrics deploymentMetrics,
            Option<X509Certificate2> manifestTrustBundle,
            TimeSpan twinPullOnConnectThrottleTime)
        {
            this.desiredPropertiesSerDe = Preconditions.CheckNotNull(desiredPropertiesSerDe, nameof(desiredPropertiesSerDe));
            this.deploymentConfigInfo = Option.None<DeploymentConfigInfo>();
            this.reportedProperties = Option.None<TwinCollection>();
            this.moduleConnection = new ModuleConnection(moduleClientProvider, requestManager, this.OnConnectionStatusChanged, this.OnDesiredPropertiesUpdated, enableSubscriptions);
            this.retryStrategy = Preconditions.CheckNotNull(retryStrategy, nameof(retryStrategy));
            this.refreshTwinTask = new PeriodicTask(this.ForceRefreshTwin, refreshConfigFrequency, refreshConfigFrequency, Events.Log, "refresh twin config");
            this.pullOnReconnect = enableSubscriptions;
            this.deviceManager = Preconditions.CheckNotNull(deviceManager, nameof(deviceManager));
            Events.TwinRefreshInit(refreshConfigFrequency);
            this.deploymentMetrics = Preconditions.CheckNotNull(deploymentMetrics, nameof(deploymentMetrics));
            this.initTask = this.ForceRefreshTwin();
            this.manifestTrustBundle = manifestTrustBundle;
            this.twinPullOnConnectThrottleTime = twinPullOnConnectThrottleTime;
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
                this.deploymentMetrics.ReportIotHubSync(true);
                Events.UpdatedReportedProperties();
            }
            catch (Exception e)
            {
                Events.ErrorUpdatingReportedProperties(e);
                this.deploymentMetrics.ReportIotHubSync(false);
                throw;
            }
        }

        //// public async Task SendEventBatchAsync(IEnumerable<Message> messages)
        //// {
        ////    Events.UpdatingReportedProperties();
        ////    try
        ////    {
        ////        Option<IModuleClient> moduleClient = this.moduleConnection.GetModuleClient();
        ////        if (!moduleClient.HasValue)
        ////        {
        ////            Events.SendEventClientEmpty();
        ////            return;
        ////        }

        ////        await moduleClient.ForEachAsync(d => d.SendEventBatchAsync(messages));
        ////        Events.SendEvent();
        ////    }
        ////    catch (Exception e)
        ////    {
        ////        Events.ErrorSendingEvent(e);
        ////        throw;
        ////    }
        //// }

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

        internal static void ValidateSchemaVersion(DeploymentConfig config)
        {
            string schemaVersion = config.SchemaVersion;

            if (string.IsNullOrWhiteSpace(schemaVersion) || !Version.TryParse(schemaVersion, out Version actualSchemaVersion))
            {
                throw new InvalidSchemaVersionException($"Invalid desired properties schema version {schemaVersion}");
            }

            // Check major version and upper bound
            if (actualSchemaVersion.Major != ExpectedSchemaVersion.Major ||
                actualSchemaVersion > ExpectedSchemaVersion)
            {
                throw new InvalidSchemaVersionException($"The desired properties schema version {schemaVersion} is not compatible with the expected version {ExpectedSchemaVersion}");
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

                // We want to notify the IoT Edge daemon in the following two cases -
                // 1. The device has been deprovisioned and might have been reprovisioned on another IoT hub.
                // 2. The IoT Hub that the device belongs to is no longer in existence and the device might have been
                // moved to a different IoT hub.
                //
                // When the Amqp or AmqpWs protocol is used, the SDK returns a connection status change reason of
                // Device_Disabled when a device is either disabled or deleted in IoT hub.
                // For the Mqtt and MqttWs protocol however, the SDK returns a Bad_Credential status as it's not
                // possible for IoT hub to distinguish between 'device does not exist', 'device is disabled' and
                // 'device exists but wrong credentials were supplied' cases.
                //
                // When an IoT hub is no longer in existence (i.e., it has been deleted), the SDK returns the
                // connection status change reason of Bad_Credential for all the Amqp and Mqtt protocols.
                if ((reason == ConnectionStatusChangeReason.Device_Disabled &&
                    (protocol == UpstreamProtocol.Amqp || protocol == UpstreamProtocol.AmqpWs)) ||
                    reason == ConnectionStatusChangeReason.Bad_Credential)
                {
                    await this.deviceManager.ReprovisionDeviceAsync();
                }

                if (this.pullOnReconnect && this.initTask.IsCompleted && status == ConnectionStatus.Connected)
                {
                    var delayedTwinPull = true;
                    using (await this.twinLock.LockAsync())
                    {
                        var now = DateTime.Now;
                        if (now - this.lastTwinPullOnConnect > this.twinPullOnConnectThrottleTime && !this.isDelayedTwinPullInProgress.Get())
                        {
                            this.lastTwinPullOnConnect = now;
                            await this.RefreshTwinAsync();
                            delayedTwinPull = false;
                        }
                    }

                    if (delayedTwinPull)
                    {
                        if (this.isDelayedTwinPullInProgress.GetAndSet(true))
                        {
                            Interlocked.Increment(ref this.pullRequestCounter);
                        }
                        else
                        {
                            _ = this.DelayedRefreshTwinAsync();
                        }
                    }
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.ConnectionStatusChangedHandlingError(ex);
            }
        }

        async Task DelayedRefreshTwinAsync()
        {
            Events.StartedDelayedTwinPull();
            await Task.Delay(this.twinPullOnConnectThrottleTime);

            var requestCounter = default(int);
            using (await this.twinLock.LockAsync())
            {
                this.lastTwinPullOnConnect = DateTime.Now;

                try
                {
                    await this.RefreshTwinAsync();
                }
                catch
                {
                    // swallowing intentionally
                }

                requestCounter = Interlocked.Exchange(ref this.pullRequestCounter, 0);

                this.isDelayedTwinPullInProgress.Set(false);
            }

            if (requestCounter > PullFrequencyThreshold)
            {
                Events.PullingTwinHasBeenTriggeredFrequently(requestCounter, Convert.ToInt32(this.twinPullOnConnectThrottleTime.TotalSeconds));
            }

            Events.FinishedDelayedTwinPull();
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
                        Events.LogDesiredPropertiesAfterFullTwin(twin.Properties.Desired);
                        if (this.CheckIfTwinSignatureIsValid(twin.Properties.Desired))
                        {
                            this.desiredProperties = Option.Some(twin.Properties.Desired);
                            await this.UpdateDeploymentConfig(twin.Properties.Desired);
                            this.reportedProperties = Option.Some(twin.Properties.Reported);
                            Events.TwinRefreshSuccess();
                        }
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        this.deploymentConfigInfo = Option.Some(new DeploymentConfigInfo(this.desiredProperties.Map(d => d.Version).GetOrElse(0), ex));
                        Events.TwinRefreshError(ex);
                    }
                });
        }

        async Task<Option<Twin>> GetTwinFromIoTHub(bool retrying = false)
        {
            IModuleClient moduleClient = null;

            try
            {
                async Task<Twin> GetTwinFunc()
                {
                    Events.GettingModuleClient(retrying);
                    moduleClient = await this.moduleConnection.GetOrCreateModuleClient();
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
                this.deploymentMetrics.ReportIotHubSync(true);
                return Option.Some(twin);
            }
            catch (Exception e)
            {
                Events.ErrorGettingTwin(e);
                this.deploymentMetrics.ReportIotHubSync(false);

                if (!retrying && moduleClient != null)
                {
                    try
                    {
                        await moduleClient.CloseAsync();
                    }
                    catch (Exception e2)
                    {
                        Events.ErrorClosingModuleClientForRetry(e2);
                    }

                    return await this.GetTwinFromIoTHub(true);
                }

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
                Events.LogDesiredPropertiesAfterPatch(desiredProperties);
                if (this.CheckIfTwinSignatureIsValid(desiredProperties))
                {
                    this.desiredProperties = Option.Some(desiredProperties);
                    await this.UpdateDeploymentConfig(desiredProperties);
                    Events.DesiredPropertiesPatchApplied();
                }
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
                ValidateSchemaVersion(deploymentConfig);
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

        internal bool CheckIfTwinSignatureIsValid(TwinCollection twinDesiredProperties)
        {
            // This function call returns false only when the signature verification fails
            // It returns true when there is no signature data or when the signature is verified
            if (!this.CheckIfManifestSigningIsEnabled(twinDesiredProperties))
            {
                Events.ManifestSigningIsNotEnabled();
            }
            else
            {
                Events.ManifestSigningIsEnabled();
                if (this.ExtractAgentTwinAndVerify(twinDesiredProperties))
                {
                    Events.VerifyTwinSignatureSuceeded();
                }
                else
                {
                    Events.VerifyTwinSignatureFailed();
                    return false;
                }
            }

            return true;
        }

        internal bool CheckIfManifestSigningIsEnabled(TwinCollection twinDesiredProperties)
        {
            // If there is no integrity section in the desired twin properties and the manifest trust bundle is not configured then manifest signing is turned off
            // If we have integrity section or the configuration of manifest trust bundle then manifest signing is turned on
            JToken integrity = JObject.Parse(twinDesiredProperties.ToString())["integrity"];
            bool hasIntegrity = integrity != null && integrity.HasValues;
            bool hasManifestCA = this.manifestTrustBundle.HasValue;
            this.deploymentMetrics.ReportManifestIntegrity(hasManifestCA, hasIntegrity);
            return hasManifestCA || hasIntegrity;
        }

        internal bool ExtractAgentTwinAndVerify(TwinCollection twinDesiredProperties)
        {
            try
            {
                // Extract Desired properties
                JObject desiredProperties = new JObject();
                JObject twinJobject = JObject.Parse(twinDesiredProperties.ToString());
                desiredProperties["modules"] = twinJobject["modules"];
                desiredProperties["runtime"] = twinJobject["runtime"];
                desiredProperties["schemaVersion"] = twinJobject["schemaVersion"];
                desiredProperties["systemModules"] = twinJobject["systemModules"];

                // Check if Manifest Trust Bundle is configured
                X509Certificate2 manifestTrustBundleRootCertificate;
                if (!this.manifestTrustBundle.HasValue && twinJobject["integrity"] == null)
                {
                    // Actual code path would never get here as we check enablement before this. Added for Unit test purpose only.
                    Events.ManifestSigningIsNotEnabled();
                    throw new ManifestSigningIsNotEnabledProperly("Manifest Signing is Disabled.");
                }
                else if (!this.manifestTrustBundle.HasValue && twinJobject["integrity"] != null)
                {
                    Events.ManifestTrustBundleIsNotConfigured();
                    throw new ManifestSigningIsNotEnabledProperly("Deployment manifest is signed but the Manifest Trust bundle is not configured. Please configure in config.toml");
                }
                else if (this.manifestTrustBundle.HasValue && twinJobject["integrity"] == null)
                {
                    Events.DeploymentManifestIsNotSigned();
                    throw new ManifestSigningIsNotEnabledProperly("Manifest Trust bundle is configured but the Deployment manifest is not signed. Please sign it.");
                }
                else
                {
                    // deployment manifest is signed and also the manifest trust bundle is configured
                    manifestTrustBundleRootCertificate = this.manifestTrustBundle.OrDefault();
                }

                // Extract Integrity header section
                JToken integrity = twinJobject["integrity"];
                JToken header = integrity["header"];

                // Extract Signer Cert section
                JToken signerCertJtoken = integrity["header"]["signercert"];
                string signerCombinedCert = signerCertJtoken.Aggregate(string.Empty, (res, next) => res + next);
                X509Certificate2 signerCert = new X509Certificate2(Convert.FromBase64String(signerCombinedCert));

                // Extract Intermediate CA Cert section
                JToken intermediatecacertJtoken = integrity["header"]["intermediatecacert"];
                string intermediatecacertCombinedCert = signerCertJtoken.Aggregate(string.Empty, (res, next) => res + next);
                X509Certificate2 intermediatecacert = new X509Certificate2(Convert.FromBase64String(intermediatecacertCombinedCert));

                // Extract Signature bytes and algorithm section
                JToken signature = integrity["signature"]["bytes"];
                byte[] signatureBytes = Convert.FromBase64String(signature.ToString());
                JToken algo = integrity["signature"]["algorithm"];
                string algoStr = algo.ToString();
                KeyValuePair<string, HashAlgorithmName> algoResult = SignatureValidator.ParseAlgorithm(algoStr);

                // Extract the manifest trust bundle certificate and verify chaining
                bool signatureVerified = false;
                using (IDisposable verificationTimer = this.deploymentMetrics.StartTwinSignatureTimer())
                {
                    // Currently not verifying the chaining as Manifest signer client already does that.
                    // There is known bug in which the certs are not processed correctly which breaks the chaining verification.
                    /* if (!CertificateHelper.VerifyManifestTrustBunldeCertificateChaining(signerCert, intermediatecacert, manifestTrustBundleRootCertificate))
                    {
                        throw new ManifestTrustBundleChainingFailedException("The signer cert with or without the intermediate CA cert in the twin does not chain up to the Manifest Trust Bundle Root CA configured in the device");
                    }
                    */

                    signatureVerified = SignatureValidator.VerifySignature(desiredProperties.ToString(), header.ToString(), signatureBytes, signerCert, algoResult.Key, algoResult.Value);
                }

                this.deploymentMetrics.ReportTwinSignatureResult(signatureVerified, algoStr);
                if (signatureVerified)
                {
                    Events.ExtractAgentTwinSucceeded();
                }

                return signatureVerified;
            }
            catch (Exception ex)
            {
                this.deploymentMetrics.ReportTwinSignatureResult(false);
                Events.ExtractAgentTwinAndVerifyFailed(ex);
                throw ex;
            }
        }

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
                ErrorClosingModuleClient,
                LogDesiredPropertiesAfterPatch,
                LogDesiredPropertiesAfterFullTwin,
                ExtractAgentTwinAndVerifyFailed,
                ExtractAgentTwinSucceeded,
                VerifyTwinSignatureFailed,
                VerifyTwinSignatureSuceeded,
                VerifyTwinSignatureException,
                ManifestSigningIsEnabled,
                ManifestSigningIsNotEnabled,
                ManifestTrustBundleIsNotConfigured,
                DeploymentManifestIsNotSigned,
                PullingTwinHasBeenTriggeredFrequently,
                StartedDelayedTwinPull,
                FinishedDelayedTwinPull
            }

            public static void DesiredPropertiesPatchFailed(Exception exception)
            {
                Log.LogError((int)EventIds.DesiredPropertiesFailed, exception, "Edge agent failed to process desired properties update patch");
            }

            public static void ConnectionStatusChanged(ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                Log.LogDebug((int)EventIds.ConnectionStatusChanged, $"Connection status changed to {status} with reason {reason}");
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

            public static void GettingModuleClient(bool retrying)
            {
                Log.LogDebug((int)EventIds.GettingModuleClient, $"Getting module client to refresh the twin with retrying set to {retrying}");
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

            public static void ErrorClosingModuleClientForRetry(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorClosingModuleClient, e, "Error closing module client for retry");
            }

            internal static void LogDesiredPropertiesAfterPatch(TwinCollection twinCollection)
            {
                Log.LogTrace((int)EventIds.LogDesiredPropertiesAfterPatch, $"Obtained desired properties after apply patch: {twinCollection}");
            }

            internal static void LogDesiredPropertiesAfterFullTwin(TwinCollection twinCollection)
            {
                Log.LogTrace((int)EventIds.LogDesiredPropertiesAfterFullTwin, $"Obtained desired properites after processing full twin: {twinCollection}");
            }

            internal static void ExtractAgentTwinAndVerifyFailed(Exception exception)
            {
                Log.LogError((int)EventIds.ExtractAgentTwinAndVerifyFailed, exception, "Extract Edge agent twin and verify failed");
            }

            internal static void ExtractAgentTwinSucceeded()
            {
                Log.LogDebug((int)EventIds.ExtractAgentTwinSucceeded, "Successfully Extracted twin for signature verification");
            }

            internal static void VerifyTwinSignatureException(Exception exception)
            {
                Log.LogError((int)EventIds.VerifyTwinSignatureException, exception, "Verify Twin Signature Failed Exception");
            }

            internal static void VerifyTwinSignatureFailed()
            {
                Log.LogError((int)EventIds.VerifyTwinSignatureFailed, "Twin Signature is not verified");
            }

            internal static void VerifyTwinSignatureSuceeded()
            {
                Log.LogInformation((int)EventIds.VerifyTwinSignatureSuceeded, "Twin Signature is verified");
            }

            internal static void ManifestSigningIsEnabled()
            {
                Log.LogDebug((int)EventIds.ManifestSigningIsEnabled, $"Manifest Signing is enabled");
            }

            internal static void ManifestSigningIsNotEnabled()
            {
                Log.LogDebug((int)EventIds.ManifestSigningIsNotEnabled, $"Manifest Signing is not enabled. To enable, sign the deployment manifest and also enable manifest trust bundle in certificate client");
            }

            internal static void ManifestTrustBundleIsNotConfigured()
            {
                Log.LogWarning((int)EventIds.ManifestTrustBundleIsNotConfigured, $"Deployment manifest is signed but the Manifest Trust bundle is not configured. Please configure in config.toml");
            }

            internal static void DeploymentManifestIsNotSigned()
            {
                Log.LogWarning((int)EventIds.DeploymentManifestIsNotSigned, $"Manifest Trust bundle is configured but the Deployment manifest is not signed. Please sign it.");
            }

            internal static void PullingTwinHasBeenTriggeredFrequently(int count, int seconds)
            {
                Log.LogWarning((int)EventIds.PullingTwinHasBeenTriggeredFrequently, $"Pulling twin by 'Connected' event has been triggered frequently, {count} times in the last {seconds} seconds. This can be a sign when more edge devices use the same identity and they keep getting disconnected.");
            }

            internal static void StartedDelayedTwinPull()
            {
                Log.LogDebug((int)EventIds.StartedDelayedTwinPull, $"Started delayed twin-pull");
            }

            internal static void FinishedDelayedTwinPull()
            {
                Log.LogDebug((int)EventIds.FinishedDelayedTwinPull, $"Finished delayed twin-pull");
            }
        }
    }
}
