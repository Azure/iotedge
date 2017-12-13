// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunners
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class OrderedRetryPlanRunner : IPlanRunner
    {
        AsyncLock sync;
        long lastDeploymentId;
        Dictionary<string, (int RunCount, DateTime LastRunTimeUtc)> commandRunStatus;
        readonly int maxRunCount;
        readonly int coolOffTimeUnitInSeconds;
        readonly ISystemTime systemTime;

        public OrderedRetryPlanRunner(int maxRunCount, int coolOffTimeUnitInSeconds, ISystemTime systemTime)
        {
            this.maxRunCount = Preconditions.CheckRange(
                maxRunCount, 1, nameof(maxRunCount)
            );
            this.coolOffTimeUnitInSeconds = Preconditions.CheckRange(
                coolOffTimeUnitInSeconds, 0, nameof(coolOffTimeUnitInSeconds)
            );
            this.systemTime = Preconditions.CheckNotNull(systemTime, nameof(systemTime));
            this.sync = new AsyncLock();
            this.lastDeploymentId = -1;
            this.commandRunStatus = new Dictionary<string, (int RunCount, DateTime LastRunTimeUtc)>();
        }

        public async Task ExecuteAsync(long deploymentId, Plan plan, CancellationToken token)
        {
            Preconditions.CheckRange(deploymentId, 1, nameof(deploymentId));
            Preconditions.CheckNotNull(plan, nameof(plan));

            using (await this.sync.LockAsync())
            {
                Events.PlanExecStarted(deploymentId);

                // if this is a new deployment we haven't seen before then clear the
                // saved command run status values
                if (this.lastDeploymentId != -1 && this.lastDeploymentId != deploymentId)
                {
                    Events.NewDeployment(deploymentId);
                    this.commandRunStatus.Clear();
                }
                else
                {
                    if (this.lastDeploymentId != -1)
                    {
                        Events.OldDeployment(deploymentId);
                    }
                    else
                    {
                        Events.NewDeployment(deploymentId);
                    }
                }

                // update saved deployment ID
                this.lastDeploymentId = deploymentId;

                Option<List<Exception>> failures = Option.None<List<Exception>>();

                foreach (ICommand command in plan.Commands)
                {
                    try
                    {
                        if (token.IsCancellationRequested)
                        {
                            Events.PlanExecCancelled(deploymentId);
                            break;
                        }

                        var (shouldRun, runCount, coolOffPeriod, elapsedTime) = this.ShouldRunCommand(command);
                        if (shouldRun)
                        {
                            await command.ExecuteAsync(token);

                            // since this command ran successfully reset its
                            // run status
                            if (this.commandRunStatus.ContainsKey(command.Id))
                            {
                                this.commandRunStatus[command.Id] = (0, this.systemTime.UtcNow);
                            }
                        }
                        else
                        {
                            Events.SkippingCommand(deploymentId, command, runCount, this.maxRunCount, coolOffPeriod, elapsedTime);
                        }
                    }
                    catch (Exception ex) when (ex.IsFatal() == false)
                    {
                        Events.PlanExecStepFailed(deploymentId, command);
                        if (!failures.HasValue)
                        {
                            failures = Option.Some(new List<Exception>());
                        }
                        failures.ForEach(f => f.Add(ex));

                        // since this command failed, record its status
                        int runCount = 0;
                        if (this.commandRunStatus.ContainsKey(command.Id))
                        {
                            runCount = this.commandRunStatus[command.Id].RunCount;
                        }
                        this.commandRunStatus[command.Id] = (runCount + 1, this.systemTime.UtcNow);
                    }
                }

                Events.PlanExecEnded(deploymentId);
                failures.ForEach(f => throw new AggregateException(f));
            }
        }

        private
        (
            bool shouldRun,
            int runCount,
            TimeSpan coolOffPeriod,
            TimeSpan elapsedTime
        ) ShouldRunCommand(ICommand command)
        {
            // the command should be run if there's no entry for it in our status dictionary
            if (this.commandRunStatus.ContainsKey(command.Id) == false)
            {
                return (true, -1, TimeSpan.MinValue, TimeSpan.MinValue);
            }

            var (runCount, lastRunTimeUtc) = this.commandRunStatus[command.Id];

            // if this command has been run maxRunCount times already then don't
            // run it anymore
            if (runCount == this.maxRunCount)
            {
                return (false, runCount, TimeSpan.MinValue, TimeSpan.MinValue);
            }

            TimeSpan coolOffPeriod = TimeSpan.FromSeconds(
                this.coolOffTimeUnitInSeconds * Math.Pow(2, runCount)
            );
            TimeSpan elapsedTime = this.systemTime.UtcNow - lastRunTimeUtc;

            return (elapsedTime > coolOffPeriod, runCount, coolOffPeriod, elapsedTime);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<OrderedRetryPlanRunner>();
            const int IdStart = AgentEventIds.OrderedRetryPlanRunner;

            enum EventIds
            {
                PlanExecStarted = IdStart,
                NewDeployment,
                OldDeployment,
                SkippingCommand,
                PlanExecStepFailed,
                PlanExecEnded,
                Cancelled,
            }

            public static void PlanExecStarted(long deploymentId) =>
                Log.LogInformation((int)EventIds.PlanExecStarted, $"Plan execution started for deployment {deploymentId}");

            public static void PlanExecCancelled(long deploymentId) =>
                Log.LogInformation((int)EventIds.Cancelled, $"Plan execution for deployment {deploymentId} was cancelled");

            public static void NewDeployment(long deploymentId) =>
                Log.LogDebug((int)EventIds.NewDeployment, $"Received new deployment {deploymentId}");

            public static void OldDeployment(long deploymentId) =>
                Log.LogDebug((int)EventIds.OldDeployment, $"Running plan on existing deployment {deploymentId}");

            public static void SkippingCommand(long deploymentId, ICommand command, int runCount, int maxRunCount, TimeSpan coolOffPeriod, TimeSpan elapsedTime)
            {
                string msg = $"Skipping command \"{command.Show()}\" in deployment {deploymentId}.";
                if (runCount == maxRunCount)
                {
                    msg += $" Command has been tried {runCount} times. Max run count is {maxRunCount}.";
                }
                else
                {
                    msg += $" Will retry in {(coolOffPeriod - elapsedTime).Humanize()}";
                }

                Log.LogInformation((int)EventIds.SkippingCommand, msg);
            }

            public static void PlanExecStepFailed(long deploymentId, ICommand command) =>
                Log.LogError((int)EventIds.PlanExecStepFailed, $"Step failed in deployment {deploymentId}, continuing execution. Failure when running command {command.Show()}");

            public static void PlanExecEnded(long deploymentId)
            {
                Log.LogInformation((int)EventIds.PlanExecEnded, $"Plan execution ended for deployment {deploymentId}");
            }
        }
    }
}
