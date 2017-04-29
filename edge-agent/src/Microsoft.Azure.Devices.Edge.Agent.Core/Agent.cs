// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

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
        }

        public async Task ReconcileAsync(CancellationToken token)
        {
            ModuleSet current = await this.environment.GetModulesAsync(token);
            Plan plan = this.planner.Plan(this.desired, current);

            if (!plan.IsEmpty)
            {
                await plan.ExecuteAsync(token);
            }
        }

        public async Task ReconcileAsync(Diff diff, CancellationToken token)
        {
            using (await this.sync.LockAsync(token))
            {
                ModuleSet snapshot = this.desired.Value;
                ModuleSet updated = snapshot.ApplyDiff(diff);
                if (!this.desired.CompareAndSet(snapshot, updated))
                {
                    throw new InvalidOperationException("Invalid update desired moduleset operation.");
                }
            }
            await this.ReconcileAsync(token);
        }
    }
}