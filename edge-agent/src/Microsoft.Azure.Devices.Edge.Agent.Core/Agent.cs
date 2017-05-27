// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class Agent
    {
        readonly AtomicReference<ModuleSet> desired;
        readonly IEnvironment environment;
        readonly IPlanner planner;
        readonly AsyncLock sync;

        public Agent(ModuleSet initial, IEnvironment environment, IPlanner planner)
        {
            this.desired = new AtomicReference<ModuleSet>(Preconditions.CheckNotNull(initial, nameof(initial)));
            this.environment = Preconditions.CheckNotNull(environment, nameof(environment));
            this.planner = Preconditions.CheckNotNull(planner, nameof(planner));
            this.sync = new AsyncLock();
            Events.AgentCreated();
        }

        public static async Task<Agent> CreateAsync(IConfigSource config, IEnvironment environment, IPlanner planner)
        {
            ModuleSet initial = await config.GetConfigAsync();
            return new Agent(initial, environment, planner);
        }

        public async Task ReconcileAsync(CancellationToken token)
        {
            ModuleSet current = await this.environment.GetModulesAsync(token);
            Plan plan = this.planner.Plan(this.desired, current);

            if (!plan.IsEmpty)
            {
                try
                {
                    await plan.ExecuteAsync(token);
                }
                catch (Exception ex)
                {
                    Events.PlanExecutionFailed(ex);
                    throw;
                }
            }
        }

        public async Task ApplyDiffAsync(Diff diff, CancellationToken token)
        {
            using (await this.sync.LockAsync(token))
            {
                ModuleSet snapshot = this.desired.Value;
                ModuleSet updated = snapshot.ApplyDiff(diff);
                if (!this.desired.CompareAndSet(snapshot, updated))
                {
                    Events.UpdateDesiredStateFailed();
                    throw new InvalidOperationException("Invalid update desired moduleset operation.");
                }
            }
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