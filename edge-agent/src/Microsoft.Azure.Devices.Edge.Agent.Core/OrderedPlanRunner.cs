// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class OrderedPlanRunner : IPlanRunner
    {
        public async Task ExecuteAsync(Plan plan, CancellationToken token)
        {
            Option<List<Exception>> failures = Option.None<List<Exception>>();
            Events.PlanExecStarted();
            foreach (ICommand command in plan.Commands)
            {
                // TODO add rollback on failure?
                try
                {
                    await command.ExecuteAsync(token);
                }
                catch (Exception ex)
                {
                    Events.PlanExecStepFailed(command);
                    if (!failures.HasValue)
                    {
                        failures = Option.Some(new List<Exception>());
                    }
                    failures.ForEach(f => f.Add(ex));
                }
            }
            failures.ForEach(f => throw new AggregateException(f));
            Events.PlanExecEnded();
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<OrderedPlanRunner>();
            const int IdStart = AgentEventIds.PlanRunner;

            enum EventIds
            {
                PlanExecStarted = IdStart,
                PlanExecStepFailed,
                PlanExecEnded
            }

            public static void PlanExecStarted()
            {
                Log.LogInformation((int)EventIds.PlanExecStarted, "Plan execution started");
            }

            public static void PlanExecStepFailed(ICommand command)
            {
                Log.LogError((int)EventIds.PlanExecStepFailed, $"Step failed, continuing execution. Failure on {command.Show()}");
            }

            public static void PlanExecEnded()
            {
                Log.LogInformation((int)EventIds.PlanExecEnded, "Plan execution ended");
            }
        }
    }
}
