// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class Plan
    {
        readonly ImmutableList<ICommand> commands;

        public static Plan Empty { get; } = new Plan(ImmutableList<ICommand>.Empty);

        public bool IsEmpty => this.commands.IsEmpty;

        public Plan(IList<ICommand> commands)
        {
            this.commands = Preconditions.CheckNotNull(commands.ToImmutableList(), nameof(commands));
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            Events.PlanExecStarted();
            foreach (ICommand command in this.commands)
            {
                // TODO add rollback on failure?
                await command.ExecuteAsync(token);
            }
            Events.PlanExecEnded();
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<Plan>();
            const int IdStart = AgentEventIds.Plan;

            enum EventIds
            {
                PlanExecStarted = IdStart,
                PlanExecEnded
            }

            public static void PlanExecStarted()
            {
                Log.LogInformation((int)EventIds.PlanExecStarted, "Plan execution started");
            }

            public static void PlanExecEnded()
            {
                Log.LogInformation((int)EventIds.PlanExecEnded, "Plan execution ended");
            }
        }
    }



}