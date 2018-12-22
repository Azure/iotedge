// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class OrderedPlanRunner : IPlanRunner
    {
        public async Task<bool> ExecuteAsync(long deploymentId, Plan plan, CancellationToken token)
        {
            Option<List<Exception>> failures = Option.None<List<Exception>>();
            Events.PlanExecStarted(deploymentId);
            foreach (ICommand command in plan.Commands)
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        Events.PlanExecCancelled(deploymentId);
                        break;
                    }

                    await command.ExecuteAsync(token);
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    Events.PlanExecStepFailed(deploymentId, command);
                    if (!failures.HasValue)
                    {
                        failures = Option.Some(new List<Exception>());
                    }
                    failures.ForEach(f => f.Add(ex));
                }
            }

            Events.PlanExecEnded(deploymentId);
            failures.ForEach(f => throw new AggregateException(f));
            return true;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<OrderedPlanRunner>();
            const int IdStart = AgentEventIds.OrderedPlanRunner;

            enum EventIds
            {
                PlanExecStarted = IdStart,
                PlanExecStepFailed,
                PlanExecEnded,
                Cancelled,
            }

            public static void PlanExecStarted(long deploymentId) =>
                Log.LogInformation((int)EventIds.PlanExecStarted, $"Plan execution started for deployment {deploymentId}");

            public static void PlanExecCancelled(long deploymentId) =>
                Log.LogInformation((int)EventIds.Cancelled, $"Plan execution for deployment {deploymentId} was cancelled");

            public static void PlanExecStepFailed(long deploymentId, ICommand command) =>
                Log.LogError((int)EventIds.PlanExecStepFailed, $"Step failed in deployment {deploymentId}, continuing execution. Failure on {command.Show()}");

            public static void PlanExecEnded(long deploymentId) =>
                Log.LogInformation((int)EventIds.PlanExecEnded, $"Plan execution ended for deployment {deploymentId}");
        }
    }
}
