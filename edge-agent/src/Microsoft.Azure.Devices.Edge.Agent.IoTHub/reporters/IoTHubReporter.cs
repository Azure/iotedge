// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    public class IoTHubReporter : IReporter
    {
        const string CurrentReportedPropertiesSchemaVersion = "1.0";

        readonly IEdgeAgentConnection edgeAgentConnection;
        readonly IEnvironment environment;
        readonly AsyncLock sync;
        Option<AgentState> reportedState;
        readonly ISerde<AgentState> agentStateSerde;
        readonly VersionInfo versionInfo;

        public IoTHubReporter(
            IEdgeAgentConnection edgeAgentConnection,
            IEnvironment environment,
            ISerde<AgentState> agentStateSerde,
            VersionInfo versionInfo
        )
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            this.environment = Preconditions.CheckNotNull(environment, nameof(environment));
            this.agentStateSerde = Preconditions.CheckNotNull(agentStateSerde, nameof(agentStateSerde));
            this.versionInfo = Preconditions.CheckNotNull(versionInfo, nameof(versionInfo));

            this.sync = new AsyncLock();
            this.reportedState = Option.None<AgentState>();
        }

        async Task<Option<AgentState>> GetReportedStateAsync()
        {
            try
            {
                this.reportedState = this.reportedState
                    .Else(() => this.edgeAgentConnection.ReportedProperties.Map(coll => this.agentStateSerde.Deserialize(coll.ToJson())));
                return this.reportedState;
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                // An exception thrown here implies that the AgentState Deserialization failed,
                // which implies the current ReportedProperties can't be transformed into AgentState,
                // delete the current reported state
                Option<TwinCollection> deletionPatch = this.MakeDeletionPatch(this.edgeAgentConnection.ReportedProperties);
                Events.ClearedReportedProperties(ex);
                await deletionPatch.ForEachAsync(patch => this.edgeAgentConnection.UpdateReportedPropertiesAsync(patch));
                return Option.Some(AgentState.Empty);
            }

        }

        Option<TwinCollection> MakeDeletionPatch(Option<TwinCollection> reportedProperties)
        {
            return reportedProperties.Map(
                coll =>
                {
                    var emptyCollection = new TwinCollection();
                    foreach (KeyValuePair<string, object> section in coll)
                    {
                        emptyCollection[section.Key] = null;
                    }

                    return emptyCollection;
                });
        }

        void SetReported(AgentState reported)
        {
            if (this.reportedState.OrDefault() != reported)
            {
                this.reportedState = Option.Some(reported);
            }
        }

        async Task<
        (
            Option<AgentState> reportedState,
            Option<AgentState> currentState
        )> BuildStatesAsync(CancellationToken token, ModuleSet moduleSet, DeploymentConfigInfo deploymentConfigInfo, DeploymentStatus status)
        {
            // produce JSONs for previously reported state and current state
            Option<AgentState> agentState = await this.GetReportedStateAsync();
            Option<AgentState> currentState = Option.None<AgentState>();

            // if there is no reported JSON to compare against, then we don't do anything
            // because this typically means that we never connected to IoT Hub before and
            // we have no connection yet
            return await agentState.Match(async rs =>
            {
                // build system module objects
                IEdgeAgentModule edgeAgentModule = await this.environment.GetEdgeAgentModuleAsync(token);
                IEdgeHubModule edgeHubModule = (moduleSet?.Modules?.ContainsKey(Constants.EdgeHubModuleName) ?? false)
                    ? moduleSet.Modules[Constants.EdgeHubModuleName] as IEdgeHubModule
                    : UnknownEdgeHubModule.Instance;
                edgeHubModule = edgeHubModule ?? UnknownEdgeHubModule.Instance;

                IImmutableDictionary<string, IModule> userModules =
                    moduleSet?.Modules?.Remove(edgeAgentModule.Name)?.Remove(edgeHubModule.Name) ??
                    ImmutableDictionary<string, IModule>.Empty;

                currentState = Option.Some(new AgentState(
                    deploymentConfigInfo?.Version ?? rs.LastDesiredVersion,
                    status,
                    deploymentConfigInfo != null ? (await this.environment.GetUpdatedRuntimeInfoAsync(deploymentConfigInfo.DeploymentConfig.Runtime)) : rs.RuntimeInfo,
                    new SystemModules(edgeAgentModule, edgeHubModule),
                    userModules.ToImmutableDictionary(),
                    CurrentReportedPropertiesSchemaVersion,
                    this.versionInfo
                ));

                return (agentState, currentState);
            },
            () =>
            {
                Events.NoSavedReportedProperties();
                return Task.FromResult((agentState, currentState));
            });
        }

        public async Task ReportAsync(CancellationToken token, ModuleSet moduleSet, DeploymentConfigInfo deploymentConfigInfo, DeploymentStatus status)
        {
            Preconditions.CheckNotNull(status, nameof(status));

            Option<AgentState> agentState = Option.None<AgentState>();
            Option<AgentState> currentState = Option.None<AgentState>();
            using (await this.sync.LockAsync(token))
            {
                try
                {
                    (agentState, currentState) = await this.BuildStatesAsync(token, moduleSet, deploymentConfigInfo, status);
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    Events.BuildStateFailed(ex);

                    // something failed during the patch generation process; we do best effort
                    // error reporting by sending a minimal patch with just the error information
                    JObject patch = JObject.FromObject(new
                    {
                        lastDesiredVersion = agentState.Map(rs => rs.LastDesiredVersion).GetOrElse(0),
                        lastDesiredStatus = new DeploymentStatus(DeploymentStatusCode.Failed, ex.Message)
                    });

                    try
                    {
                        await this.edgeAgentConnection.UpdateReportedPropertiesAsync(new TwinCollection(patch.ToString()));
                    }
                    catch (Exception ex2) when (!ex.IsFatal())
                    {
                        Events.UpdateErrorInfoFailed(ex2);
                    }
                }

                // if there is no reported JSON to compare against, then we don't do anything
                // because this typically means that we never connected to IoT Hub before and
                // we have no connection yet
                await agentState.ForEachAsync(async rs =>
                {
                    await currentState.ForEachAsync(async cs =>
                    {
                    // diff and prepare patch
                    await this.DiffAndReportAsync(cs, rs);
                    });
                }); 
            }
        }

        public async Task ReportShutdown(DeploymentStatus status, CancellationToken token)
        {
            using (await this.sync.LockAsync(token))
            {
                Preconditions.CheckNotNull(status, nameof(status));
                Option<AgentState> agentState = await this.GetReportedStateAsync();
                await agentState.ForEachAsync(state => this.ReportShutdownInternal(state, status)); 
            }
        }

        Task ReportShutdownInternal(AgentState agentState, DeploymentStatus status)
        {
            Option<IEdgeAgentModule> edgeAgentModule = agentState.SystemModules.EdgeAgent
                .Map(ea => ea as IRuntimeStatusModule)
                .Filter(ea => ea != null)
                .Map(ea => (IEdgeAgentModule)ea.WithRuntimeStatus(ModuleStatus.Unknown))
                .Else(agentState.SystemModules.EdgeAgent);

            Option<IEdgeHubModule> edgeHubModule = agentState.SystemModules.EdgeHub
                .Map(eh => eh as IRuntimeStatusModule)
                .Filter(eh => eh != null)
                .Map(eh => (IEdgeHubModule)eh.WithRuntimeStatus(ModuleStatus.Unknown))
                .Else(agentState.SystemModules.EdgeHub);

            IDictionary<string, IModule> updateUserModules = (agentState.Modules ?? ImmutableDictionary<string, IModule>.Empty)
                .Where(m => m.Key != Constants.EdgeAgentModuleName)
                .Where(m => m.Key != Constants.EdgeHubModuleName)
                .Where(m => m.Value is IRuntimeModule)
                .Select(pair =>
                {
                    IModule updatedModule = (pair.Value as IRuntimeModule)?.WithRuntimeStatus(ModuleStatus.Unknown) ?? pair.Value;
                    return new KeyValuePair<string, IModule>(pair.Key, updatedModule);
                })
                .ToDictionary(x => x.Key, x => x.Value);

            var currentState =
                new AgentState(
                    agentState.LastDesiredVersion, status, agentState.RuntimeInfo,
                    new SystemModules(edgeAgentModule, edgeHubModule),
                    updateUserModules.ToImmutableDictionary(),
                    agentState.SchemaVersion, this.versionInfo);

            return this.DiffAndReportAsync(currentState, agentState);
        }

        internal async Task DiffAndReportAsync(AgentState currentState, AgentState agentState)
        {
            try
            {
                JToken currentJson = JToken.FromObject(currentState);
                JToken reportedJson = JToken.FromObject(agentState);
                JObject patch = JsonEx.Diff(reportedJson, currentJson);

                if (patch.HasValues)
                {
                    // send reported props
                    await this.edgeAgentConnection.UpdateReportedPropertiesAsync(new TwinCollection(patch.ToString()));

                    // update our cached copy of reported properties
                    this.SetReported(currentState);

                    Events.UpdatedReportedProperties();
                }
            }
            catch (Exception e)
            {
                Events.UpdateReportedPropertiesFailed(e);

                // Swallow the exception as the device could be offline. The reported properties will get updated
                // during the next reconcile when we have connectivity.
            }
        }
    }

    static class Events
    {
        static readonly ILogger Log = Util.Logger.Factory.CreateLogger<IoTHubReporter>();
        const int IdStart = AgentEventIds.IoTHubReporter;

        enum EventIds
        {
            UpdateReportedPropertiesFailed = IdStart,
            UpdatedReportedProperties,
            NoSavedReportedProperties,
            BuildStateFailed,
            UpdateErrorInfoFailed,
            ClearedReportedProperties
        }

        public static void NoSavedReportedProperties()
        {
            Log.LogWarning((int)EventIds.NoSavedReportedProperties, "Skipped updating reported properties because no saved reported properties exist yet.");
        }

        public static void UpdatedReportedProperties()
        {
            Log.LogInformation((int)EventIds.UpdatedReportedProperties, "Updated reported properties");
        }

        public static void UpdateReportedPropertiesFailed(Exception e)
        {
            Log.LogWarning((int)EventIds.UpdateReportedPropertiesFailed, $"Updating reported properties failed with error {e.Message} type {e.GetType()}");
        }

        public static void BuildStateFailed(Exception e)
        {
            Log.LogWarning((int)EventIds.BuildStateFailed, $"Building state for computing patch failed with error {e.Message} type {e.GetType()}");
        }

        public static void UpdateErrorInfoFailed(Exception e)
        {
            Log.LogWarning((int)EventIds.UpdateErrorInfoFailed, $"Attempt to update error information while building state for computing patch failed with error {e.Message} type {e.GetType()}");
        }

        public static void ClearedReportedProperties(Exception e)
        {
            Log.LogWarning((int)EventIds.ClearedReportedProperties, $"Clearing reported properties due to error {e.Message}");
        }
    }
}
