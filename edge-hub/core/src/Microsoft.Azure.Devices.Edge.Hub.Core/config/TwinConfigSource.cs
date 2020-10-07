// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static System.FormattableString;
    using static Microsoft.Azure.Devices.Edge.Hub.Core.EdgeHubConnection;

    /// <summary>
    /// Config source that extracts EdgeHubConfig from the EdgeHub twin (via ITwinManager).
    /// </summary>
    public class TwinConfigSource : IConfigSource
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();

        readonly AsyncLock configLock = new AsyncLock();
        readonly ITwinManager twinManager;
        readonly string id;
        readonly EdgeHubConfigParser configParser;
        readonly Core.IMessageConverter<TwinCollection> twinCollectionMessageConverter;
        readonly Core.IMessageConverter<Twin> twinMessageConverter;
        readonly VersionInfo versionInfo;
        readonly EdgeHubConnection edgeHubConnection;
        Option<TwinCollection> lastDesiredProperties;

        public TwinConfigSource(
            EdgeHubConnection edgeHubConnection,
            string id,
            VersionInfo versionInfo,
            ITwinManager twinManager,
            Core.IMessageConverter<Twin> messageConverter,
            Core.IMessageConverter<TwinCollection> twinCollectionMessageConverter,
            EdgeHubConfigParser configParser)
        {
            this.edgeHubConnection = Preconditions.CheckNotNull(edgeHubConnection, nameof(edgeHubConnection));
            this.id = Preconditions.CheckNotNull(id, nameof(id));
            this.twinManager = Preconditions.CheckNotNull(twinManager, nameof(twinManager));
            this.twinMessageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.twinCollectionMessageConverter = twinCollectionMessageConverter;
            this.configParser = Preconditions.CheckNotNull(configParser, nameof(configParser));
            this.versionInfo = versionInfo ?? VersionInfo.Empty;

            this.edgeHubConnection.SetDesiredPropertiesUpdateCallback((message) => this.HandleDesiredPropertiesUpdate(message));
        }

        public event EventHandler<EdgeHubConfig> ConfigUpdated;

        public async Task<Option<EdgeHubConfig>> GetCachedConfig()
        {
            try
            {
                Option<Core.IMessage> twinMessage = await this.twinManager.GetCachedTwinAsync(this.id);

                var config = twinMessage.FlatMap(
                    (message) =>
                    {
                        Twin twin = this.twinMessageConverter.FromMessage(message);

                        if (twin.Properties.Desired.Count > 0)
                        {
                            var desiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(twin.Properties.Desired.ToJson());
                            return Option.Some(this.configParser.GetEdgeHubConfig(desiredProperties));
                        }
                        else
                        {
                            return Option.None<EdgeHubConfig>();
                        }
                    });

                return config;
            }
            catch (Exception e)
            {
                Events.FailedGettingCachedConfig(e);
                return Option.None<EdgeHubConfig>();
            }
        }

        public async Task<Option<EdgeHubConfig>> GetConfig()
        {
            using (await this.configLock.LockAsync())
            {
                return await this.GetConfigInternal();
            }
        }

        // This method updates local state and should be called only after acquiring edgeHubConfigLock
        async Task<Option<EdgeHubConfig>> GetConfigInternal()
        {
            Option<EdgeHubConfig> edgeHubConfig;
            try
            {
                Core.IMessage message = await this.twinManager.GetTwinAsync(this.id);
                Twin twin = this.twinMessageConverter.FromMessage(message);
                TwinCollection desiredProperties = twin.Properties.Desired;
                Events.LogDesiredPropertiesAfterFullTwin(desiredProperties);
                if (!CheckIfTwinPropertiesAreSigned(desiredProperties))
                {
                    Events.TwinPropertiesAreNotSigned();
                }
                else
                {
                    Events.TwinPropertiesAreSigned();
                    if (ExtractHubTwinAndVerify(desiredProperties))
                    {
                        Events.VerifyTwinSignatureSuceeded();
                    }
                    else
                    {
                        // TODO: What about updating the Edgehub config?
                        Events.VerifyTwinSignatureFailed();
                        return Option.None<EdgeHubConfig>();
                    }
                }

                this.lastDesiredProperties = Option.Some(desiredProperties);
                try
                {
                    var edgeHubDesiredProperties = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(desiredProperties.ToJson());
                    edgeHubConfig = Option.Some(this.configParser.GetEdgeHubConfig(edgeHubDesiredProperties));
                    await this.UpdateReportedProperties(twin.Properties.Desired.Version, new LastDesiredStatus(200, string.Empty));
                    Events.GetConfigSuccess();
                }
                catch (Exception ex)
                {
                    await this.UpdateReportedProperties(twin.Properties.Desired.Version, new LastDesiredStatus(400, $"Error while parsing desired properties - {ex.Message}"));
                    throw;
                }
            }
            catch (Exception ex)
            {
                edgeHubConfig = Option.None<EdgeHubConfig>();
                Events.ErrorGettingEdgeHubConfig(ex);
            }

            return edgeHubConfig;
        }

        async Task HandleDesiredPropertiesUpdate(Core.IMessage desiredPropertiesUpdate)
        {
            try
            {
                TwinCollection patch = this.twinCollectionMessageConverter.FromMessage(desiredPropertiesUpdate);
                using (await this.configLock.LockAsync())
                {
                    Option<EdgeHubConfig> edgeHubConfig = await this.lastDesiredProperties
                        .Map(baseline => this.PatchDesiredProperties(baseline, patch))
                        .GetOrElse(() => this.GetConfigInternal());

                    edgeHubConfig.ForEach(
                        config =>
                        {
                            this.ConfigUpdated?.Invoke(this, config);
                        });
                }
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingDesiredPropertiesUpdate(ex);
            }
        }

        // This method updates local state and should be called only after acquiring edgeHubConfigLock
        async Task<Option<EdgeHubConfig>> PatchDesiredProperties(TwinCollection baseline, TwinCollection patch)
        {
            LastDesiredStatus lastDesiredStatus;
            Option<EdgeHubConfig> edgeHubConfig;
            try
            {
                string desiredPropertiesJson = JsonEx.Merge(baseline, patch, true);
                var desiredProperties = new TwinCollection(desiredPropertiesJson);
                Events.LogDesiredPropertiesAfterPatch(desiredProperties);
                if (!CheckIfTwinPropertiesAreSigned(desiredProperties))
                {
                    Events.TwinPropertiesAreNotSigned();
                }
                else
                {
                    Events.TwinPropertiesAreSigned();
                    if (ExtractHubTwinAndVerify(desiredProperties))
                    {
                        Events.VerifyTwinSignatureSuceeded();
                    }
                    else
                    {
                        // TODO: What about updating the Edgehub config?
                        Events.VerifyTwinSignatureFailed();
                        lastDesiredStatus = new LastDesiredStatus(400, "Twin Signature Verification failed");
                        return Option.None<EdgeHubConfig>();
                    }
                }

                this.lastDesiredProperties = Option.Some(desiredProperties);
                var desiredPropertiesPatch = JsonConvert.DeserializeObject<EdgeHubDesiredProperties>(desiredPropertiesJson);
                edgeHubConfig = Option.Some(this.configParser.GetEdgeHubConfig(desiredPropertiesPatch));
                lastDesiredStatus = new LastDesiredStatus(200, string.Empty);
                Events.PatchConfigSuccess();
            }
            catch (Exception ex)
            {
                lastDesiredStatus = new LastDesiredStatus(400, $"Error while parsing desired properties - {ex.Message}");
                edgeHubConfig = Option.None<EdgeHubConfig>();
                Events.ErrorPatchingDesiredProperties(ex);
            }

            await this.UpdateReportedProperties(patch.Version, lastDesiredStatus);
            return edgeHubConfig;
        }

        Task UpdateReportedProperties(long desiredVersion, LastDesiredStatus desiredStatus)
        {
            try
            {
                var edgeHubReportedProperties = new ReportedProperties(this.versionInfo, desiredVersion, desiredStatus);
                var twinCollection = new TwinCollection(JsonConvert.SerializeObject(edgeHubReportedProperties));
                Core.IMessage reportedPropertiesMessage = this.twinCollectionMessageConverter.ToMessage(twinCollection);
                return this.twinManager.UpdateReportedPropertiesAsync(this.id, reportedPropertiesMessage);
            }
            catch (Exception ex)
            {
                Events.ErrorUpdatingLastDesiredStatus(ex);
                return Task.CompletedTask;
            }
        }

        private static bool CheckIfTwinPropertiesAreSigned(TwinCollection twinDesiredProperties) => JObject.Parse(twinDesiredProperties.ToString())["integrity"] != null;

        public static bool ExtractHubTwinAndVerify(TwinCollection twinDesiredProperties)
        {
            try
            {
                // Extract Desired properties
                JObject desiredProperties = new JObject();
                JObject twinJobject = JObject.Parse(twinDesiredProperties.ToString());
                desiredProperties["routes"] = twinJobject["routes"];
                desiredProperties["schemaVersion"] = twinJobject["schemaVersion"];
                desiredProperties["storeAndForwardConfiguration"] = twinJobject["storeAndForwardConfiguration"];

                // Extract Integrity section
                JToken integrity = twinJobject["integrity"];
                JToken header = integrity["header"];
                string combinedCert = integrity["header"]["cert1"].Value<string>() + integrity["header"]["cert2"].Value<string>();
                X509Certificate2 signerCert = new X509Certificate2(Convert.FromBase64String(combinedCert));
                JToken signature = integrity["signature"]["bytes"];

                // Extract Signature and algorithm
                byte[] signatureBytes = Convert.FromBase64String(signature.ToString());
                JToken algo = integrity["signature"]["algorithm"];
                string algorithmScheme = VerifyTwinSignature.GetAlgorithmScheme(algo.ToString());
                HashAlgorithmName hashAlgorithm = VerifyTwinSignature.GetHashAlgorithm(algo.ToString());
                Events.ExtractHubTwinSucceeded();

                return VerifyTwinSignature.VerifyModuleTwinSignature(desiredProperties.ToString(), header.ToString(), signatureBytes, signerCert, algorithmScheme, hashAlgorithm);
            }
            catch (Exception ex)
            {
                Events.ExtractHubTwinAndVerifyFailed(ex);
                throw ex;
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.TwinConfigSource;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinConfigSource>();

            enum EventIds
            {
                Initialized = IdStart,
                GetConfigSuccess,
                ErrorPatchingDesiredProperties,
                ErrorUpdatingLastDesiredStatus,
                ErrorHandlingDesiredPropertiesUpdate,
                PatchConfigSuccess,
                ErrorGettingCachedConfig,
                LogDesiredPropertiesAfterPatch,
                LogDesiredPropertiesAfterFullTwin,
                ExtractHubTwinAndVerifyFailed,
                ExtractHubTwinSucceeded,
                VerifyTwinSignatureFailed,
                VerifyTwinSignatureSuceeded,
                VerifyTwinSignatureException,
                TwinPropertiesAreSigned,
                TwinPropertiesAreNotSigned
            }

            internal static void ErrorGettingEdgeHubConfig(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorPatchingDesiredProperties,
                    ex,
                    Invariant($"Error getting edge hub config from twin desired properties"));
            }

            internal static void ErrorUpdatingLastDesiredStatus(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorUpdatingLastDesiredStatus,
                    ex,
                    Invariant($"Error updating last desired status for edge hub"));
            }

            internal static void ErrorHandlingDesiredPropertiesUpdate(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorHandlingDesiredPropertiesUpdate,
                    ex,
                    Invariant($"Error handling desired properties update for edge hub"));
            }

            internal static void ErrorPatchingDesiredProperties(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorPatchingDesiredProperties,
                    ex,
                    Invariant($"Error merging desired properties patch with existing desired properties for edge hub"));
            }

            internal static void GetConfigSuccess()
            {
                Log.LogInformation((int)EventIds.GetConfigSuccess, Invariant($"Obtained edge hub config from module twin"));
            }

            internal static void PatchConfigSuccess()
            {
                Log.LogInformation((int)EventIds.PatchConfigSuccess, Invariant($"Obtained edge hub config patch update from module twin"));
            }

            internal static void FailedGettingCachedConfig(Exception ex)
            {
                Log.LogWarning(
                    (int)EventIds.ErrorGettingCachedConfig,
                    ex,
                    Invariant($"Failed to get local config"));
            }

            internal static void LogDesiredPropertiesAfterPatch(TwinCollection twinCollection)
            {
                Log.LogDebug((int)EventIds.LogDesiredPropertiesAfterPatch, $"Obtained desired properties after apply patch: {twinCollection}");
            }

            internal static void LogDesiredPropertiesAfterFullTwin(TwinCollection twinCollection)
            {
                Log.LogDebug((int)EventIds.LogDesiredPropertiesAfterFullTwin, $"Obtained desired properites after processing full twin: {twinCollection}");
            }

            internal static void ExtractHubTwinAndVerifyFailed(Exception exception)
            {
                Log.LogError((int)EventIds.ExtractHubTwinAndVerifyFailed, exception, "Extract Edge Hub twin and verify failed");
            }

            internal static void ExtractHubTwinSucceeded()
            {
                Log.LogDebug((int)EventIds.ExtractHubTwinSucceeded, "Successfully Extracted twin for signature verification");
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

            internal static void TwinPropertiesAreSigned()
            {
                Log.LogDebug((int)EventIds.TwinPropertiesAreSigned, $"Twin Properties are signed");
            }

            internal static void TwinPropertiesAreNotSigned()
            {
                Log.LogDebug((int)EventIds.TwinPropertiesAreNotSigned, $"Twin Properties are not signed");
            }
        }
    }
}
