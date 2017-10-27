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

        AgentState GetReportedState()
        {
            lock (this.sync)
            {
                return this.reportedState
                    .GetOrElse(() =>
                        this.edgeAgentConnection.ReportedProperties
                            .Map(coll =>
                            {
                                return this.agentStateSerde.Deserialize(coll.ToJson());
                            })
                            .GetOrElse(null as AgentState));
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

        public async Task ReportAsync(CancellationToken token, ModuleSet moduleSet, DeploymentConfigInfo deploymentConfigInfo, DeploymentStatus status)
        {
            Preconditions.CheckNotNull(status, nameof(status));

            // produce JSONs for previously reported state and current state
            AgentState reportedState = this.GetReportedState();

            // if there is no reported JSON to compare against, then we don't do anything
            // because this typically means that we never connected to IoT Hub before and
            // we have no connection yet
            if (reportedState == null)
            {
                Events.NoSavedReportedProperties();
                return;
            }

            // build system module objects
            IEdgeAgentModule edgeAgentModule = await this.environment.GetEdgeAgentModuleAsync(token);
            IEdgeHubModule edgeHubModule = (moduleSet?.Modules?.ContainsKey(Constants.EdgeHubModuleName) ?? false)
                ? moduleSet.Modules[Constants.EdgeHubModuleName] as IEdgeHubModule
                : UnknownEdgeHubModule.Instance;
            edgeHubModule = edgeHubModule ?? UnknownEdgeHubModule.Instance;

            IImmutableDictionary<string, IModule> userModules =
                moduleSet?.Modules?.Remove(edgeAgentModule.Name)?.Remove(edgeHubModule.Name) ??
                ImmutableDictionary<string, IModule>.Empty;

            var currentState = new AgentState(
                deploymentConfigInfo?.Version ?? reportedState.LastDesiredVersion,
                status,
                deploymentConfigInfo != null ? (await this.environment.GetUpdatedRuntimeInfoAsync(deploymentConfigInfo.DeploymentConfig.Runtime)) : reportedState.RuntimeInfo,
                new SystemModules(edgeAgentModule, edgeHubModule),
                userModules.ToImmutableDictionary()
            );

            // diff and prepare patch
            JToken currentJson = JToken.FromObject(currentState);
            JToken reportedJson = JToken.FromObject(reportedState);
            JObject patch = JsonEx.Diff(reportedJson, currentJson);

            if (patch.HasValues)
            {
                try
                {
                    // send reported props
                    await this.edgeAgentConnection.UpdateReportedPropertiesAsync(new TwinCollection(patch));

                    // update our cached copy of reported properties
                    this.SetReported(currentState);

                    Events.UpdatedReportedProperties();
                }
                catch (Exception e)
                {
                    Events.UpdateReportedPropertiesFailed(e);

                    // Swallow the exception as the device could be offline. The reported properties will get updated
                    // during the next reconcile when we have connectivity.
                }
            }
            else
            {
                // if there is no difference between `currentState` and `reportedState` and
                // the saved `reportedState` is empty then we should save `currentState` as
                // the new saved reported state; this is so we avoid continuously de-serializing
                // the reported state from the twin during every reconcile
                this.SetReported(currentState);
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
            UpdatedReportedProperties = IdStart + 1,
            NoSavedReportedProperties = IdStart + 2
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
    }
}
