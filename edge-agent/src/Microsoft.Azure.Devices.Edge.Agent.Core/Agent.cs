// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class Agent
    {
        readonly IEnvironment environment;
        readonly IPlanner planner;
        readonly IReporter reporter;
        readonly IConfigSource configSource;
        readonly IModuleIdentityLifecycleManager moduleIdentityLifecycleManager;

        public Agent(IConfigSource configSource, IEnvironment environment, IPlanner planner, IReporter reporter, IModuleIdentityLifecycleManager moduleIdentityLifecycleManager)
        {
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
            this.environment = Preconditions.CheckNotNull(environment, nameof(environment));
            this.planner = Preconditions.CheckNotNull(planner, nameof(planner));
            this.reporter = Preconditions.CheckNotNull(reporter, nameof(reporter));
            this.moduleIdentityLifecycleManager = Preconditions.CheckNotNull(moduleIdentityLifecycleManager, nameof(moduleIdentityLifecycleManager));
            Events.AgentCreated();
        }

        public async Task ReconcileAsync(CancellationToken token)
        {
            Task<ModuleSet> envTask = this.environment.GetModulesAsync(token);
            Task<ModuleSet> configTask = this.configSource.GetModuleSetAsync();

            await Task.WhenAll(envTask, configTask);

            ModuleSet current = envTask.Result;
            ModuleSet desired = configTask.Result;
            ModuleSet updated = current;
           
            ImmutableDictionary<string, IModuleIdentity> identities = (await this.moduleIdentityLifecycleManager.UpdateModulesIdentity(desired, current))
                .ToImmutableDictionary(p => p.Name);
            Plan plan = await this.planner.PlanAsync(desired, current, identities);

            if (!plan.IsEmpty)
            {
                try
                {
                    await plan.ExecuteAsync(token);
                    updated = await this.environment.GetModulesAsync(token);
                }
                catch (Exception ex)
                {
                    Events.PlanExecutionFailed(ex);
                    throw;
                }
            }

            await this.reporter.ReportAsync(updated);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<Agent>();
            const int IdStart = AgentEventIds.Agent;

            enum EventIds
            {
                AgentCreated = IdStart,
                UpdateDesiredStateFailed,
                PlanExecutionFailed
            }

            public static void AgentCreated()
            {
                Log.LogDebug((int)EventIds.AgentCreated, "Agent Created.");
            }

            public static void UpdateDesiredStateFailed()
            {
                Log.LogError((int)EventIds.UpdateDesiredStateFailed, "Agent update to desired state failed.");
            }

            public static void PlanExecutionFailed(Exception ex)
            {
                Log.LogError((int)EventIds.PlanExecutionFailed, ex, "Agent Plan execution failed.");
            }
        }

    }
}
