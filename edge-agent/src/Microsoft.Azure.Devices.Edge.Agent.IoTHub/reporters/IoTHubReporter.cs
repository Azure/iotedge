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

        public IoTHubReporter(IEdgeAgentConnection edgeAgentConnection, IEnvironment environment)
        {
            this.edgeAgentConnection = Preconditions.CheckNotNull(edgeAgentConnection, nameof(edgeAgentConnection));
            this.environment = Preconditions.CheckNotNull(environment, nameof(environment));

            this.sync = new object();
            this.reportedState = Option.None<AgentState>();
        }

        JToken GetReportedJson()
        {
            lock (this.sync)
            {
                return this.reportedState
                    .Map(s => JToken.Parse(JsonConvert.SerializeObject(s)))
                    .GetOrElse(() =>
                        this.edgeAgentConnection.ReportedProperties
                            .Map(coll => JsonEx.StripMetadata(JToken.Parse(coll.ToJson())))
                            .GetOrElse(JValue.CreateNull()));
            }
        }

        void SetReported(AgentState reported)
        {
            lock (this.sync)
            {
                this.reportedState = Option.Some(reported);
            }
        }

        public async Task ReportAsync(CancellationToken token, ModuleSet moduleSet, AgentConfig agentConfig, DeploymentStatus status)
        {
            // produce JSONs for previously reported state and current state
            JToken reportedJson = this.GetReportedJson();

            // if there is no reported JSON to compare against, then we don't do anything
            // because this typically means that we never connected to IoT Hub before and
            // we have no connection yet
            if (reportedJson.Type == JTokenType.Null)
            {
                Events.NoSavedReportedProperties();
                return;
            }

            IModule edgeAgentModule = await this.environment.GetEdgeAgentModuleAsync(token);
            IEnumerable<KeyValuePair<string, IModule>> systemModules = moduleSet.Modules
                .Where(kvp => SystemModuleNames.Contains(kvp.Value.Name))
                .Concat(new KeyValuePair<string, IModule>[] { new KeyValuePair<string, IModule>(edgeAgentModule.Name, edgeAgentModule) });
            IEnumerable<KeyValuePair<string, IModule>> userModules = moduleSet.Modules.Except(systemModules);

            var currentState = new AgentState(
                agentConfig.Version,
                status,
                agentConfig.Runtime,
                systemModules.ToImmutableDictionary(),
                userModules.ToImmutableDictionary()
            );

            // diff and prepare patch
            JToken currentJson = JToken.Parse(JsonConvert.SerializeObject(currentState));
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
