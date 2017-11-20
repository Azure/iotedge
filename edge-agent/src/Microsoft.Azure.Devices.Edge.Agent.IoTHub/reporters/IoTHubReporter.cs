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
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class IoTHubReporter : IReporter
    {
        const string CurrentReportedPropertiesSchemaVersion = "1.0";

        readonly string[] SystemModuleNames = new string[]
        {
            Constants.EdgeAgentModuleName,
            Constants.EdgeHubModuleName
        };
        readonly IEdgeAgentConnection edgeAgentConnection;
        readonly IEnvironment environment;
        readonly object sync;
        Option<AgentState> reportedState;
        readonly ISerde<AgentState> agentStateSerde;

        public IoTHubReporter(
            IEdgeAgentConnection edgeAgentConnection,
            IEnvironment environment,
            ISerde<AgentState> agentStateSerde
        )
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            this.environment = Preconditions.CheckNotNull(environment, nameof(environment));
            this.agentStateSerde = Preconditions.CheckNotNull(agentStateSerde, nameof(agentStateSerde));

            this.sync = new object();
            this.reportedState = Option.None<AgentState>();
        }

        Option<AgentState> GetReportedState()
        {
            lock (this.sync)
            {
                this.reportedState = this.reportedState
                    .Else(() => this.edgeAgentConnection.ReportedProperties.Map(coll => this.agentStateSerde.Deserialize(coll.ToJson())));
                return this.reportedState;
            }
        }

        void SetReported(AgentState reported)
        {
            lock (this.sync)
            {
                if (this.reportedState.OrDefault() != reported)
                {
                    this.reportedState = Option.Some(reported);
                }
            }
        }

        async Task<
        (
            Option<AgentState> reportedState,
            Option<AgentState> currentState
        )> BuildStatesAsync(CancellationToken token, ModuleSet moduleSet, DeploymentConfigInfo deploymentConfigInfo, DeploymentStatus status)
        {
            // produce JSONs for previously reported state and current state
            Option<AgentState> reportedState = this.GetReportedState();
            Option<AgentState> currentState = Option.None<AgentState>();

            // if there is no reported JSON to compare against, then we don't do anything
            // because this typically means that we never connected to IoT Hub before and
            // we have no connection yet
            return await reportedState.Match(async rs =>
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
                    CurrentReportedPropertiesSchemaVersion
                ));

                return (reportedState, currentState);
            },
            () =>
            {
                Events.NoSavedReportedProperties();
                return Task.FromResult((reportedState, currentState));
            });
        }

        public async Task ReportAsync(CancellationToken token, ModuleSet moduleSet, DeploymentConfigInfo deploymentConfigInfo, DeploymentStatus status)
        {
            Preconditions.CheckNotNull(status, nameof(status));

            var reportedState = Option.None<AgentState>();
            var currentState = Option.None<AgentState>();
            try
            {
                (reportedState, currentState) = await this.BuildStatesAsync(token, moduleSet, deploymentConfigInfo, status);
            }
            catch(Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Events.BuildStateFailed(ex);

                // something failed during the patch generation process; we do best effort
                // error reporting by sending a minimal patch with just the error information
                var patch = JObject.FromObject(new
                {
                    lastDesiredVersion = reportedState.Map(rs => rs.LastDesiredVersion).GetOrElse(0),
                    lastDesiredStatus = new DeploymentStatus(DeploymentStatusCode.Failed, ex.Message)
                });

                try
                {
                    await this.edgeAgentConnection.UpdateReportedPropertiesAsync(new TwinCollection(patch.ToString()));
                }
                catch(Exception ex2) when (!ExceptionEx.IsFatal(ex))
                {
                    Events.UpdateErrorInfoFailed(ex2);
                }
            }

            // if there is no reported JSON to compare against, then we don't do anything
            // because this typically means that we never connected to IoT Hub before and
            // we have no connection yet
            await reportedState.ForEachAsync(async rs =>
            {
                await currentState.ForEachAsync(async cs =>
                {
                        // diff and prepare patch
                        await DiffAndReportAsync(cs, rs);
                });
            });
        }

        public async Task ReportShutdown(DeploymentStatus status, CancellationToken token)
        {
            Preconditions.CheckNotNull(status, nameof(status));
            await this.reportedState.ForEachAsync(async (rs) =>
            {
                var edgeAgentModule = rs.SystemModules?.EdgeAgent;
                if (edgeAgentModule is IRuntimeStatusModule runtimeAgent)
                {
                    edgeAgentModule = (IEdgeAgentModule)runtimeAgent.WithRuntimeStatus(ModuleStatus.Unknown);
                }
                var edgeHubModule = rs.SystemModules?.EdgeHub;
                if (edgeHubModule is IRuntimeStatusModule runtimeHub)
                {
                    edgeHubModule = (IEdgeHubModule)runtimeHub.WithRuntimeStatus(ModuleStatus.Unknown);
                }

                IDictionary<string, IModule> updateUserModules = (rs.Modules ?? ImmutableDictionary<string, IModule>.Empty)
                    .Where(m => m.Key != Constants.EdgeAgentModuleName)
                    .Where(m => m.Key != Constants.EdgeHubModuleName)
                    .Where(m => m.Value is IRuntimeModule)
                    .Select(pair =>
                       {
                           IModule updatedModule = (pair.Value as IRuntimeModule).WithRuntimeStatus(ModuleStatus.Unknown);
                           return new KeyValuePair<string, IModule>(pair.Key, updatedModule);
                       })
                    .ToDictionary(x => x.Key, x => x.Value);

                var currentState = 
                    new AgentState(
                        rs.LastDesiredVersion, status, rs.RuntimeInfo,
                        new SystemModules((IEdgeAgentModule)edgeAgentModule, (IEdgeHubModule)edgeHubModule),
                        updateUserModules.ToImmutableDictionary(), rs.SchemaVersion);

                await DiffAndReportAsync(currentState, rs);
            });
        }

        internal async Task DiffAndReportAsync(AgentState currentState, AgentState reportedState)
        {
            try
            {
                JToken currentJson = JToken.FromObject(currentState);
                JToken reportedJson = JToken.FromObject(reportedState);
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
            UpdateErrorInfoFailed
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
    }
}
