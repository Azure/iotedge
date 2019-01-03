// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class RestartPolicyManager : IRestartPolicyManager
    {
        const int MaxCoolOffPeriodSecs = 300; // 5 mins
        readonly int maxRestartCount;
        readonly int coolOffTimeUnitInSeconds;

        public RestartPolicyManager(int maxRestartCount, int coolOffTimeUnitInSeconds)
        {
            this.maxRestartCount = Preconditions.CheckRange(maxRestartCount, 1);
            this.coolOffTimeUnitInSeconds = Preconditions.CheckRange(coolOffTimeUnitInSeconds, 0);
        }

        public ModuleStatus ComputeModuleStatusFromRestartPolicy(ModuleStatus status, RestartPolicy restartPolicy, int restartCount, DateTime lastExitTimeUtc)
        {
            // TODO: If the module state is "running", when we have health-probes implemented,
            // check whether the module is in a healthy state and update appropriately if not.

            // we don't really know what status "Unknown" means
            if (status == ModuleStatus.Unknown)
            {
                throw new ArgumentException("ModuleStatus unknown is not a valid status.", nameof(status));
            }

            if (status != ModuleStatus.Running && restartPolicy > RestartPolicy.Never)
            {
                // we need to act only if restart policy is "Always" (which means the module
                // state doesn't matter - it just needs to be not "Running") or it is "OnFailure" or
                // "OnUnhealthy" (which it would be if it isn't "Always" since we'd be in this
                // "if" block  only if the restart policy was greater than "Never") and the module
                // state is "Failed" or "Unhealthy"
                if (restartPolicy == RestartPolicy.Always || (status == ModuleStatus.Failed || status == ModuleStatus.Unhealthy))
                {
                    // if restart count is >= maxRestartCount then the module "failed" - otherwise
                    // it is going to be restarted and is in "Backoff" state
                    if (restartCount >= this.maxRestartCount)
                    {
                        // if the module is "Unhealthy" this will transition it to "Failed"
                        return ModuleStatus.Failed;
                    }
                    else
                    {
                        return ModuleStatus.Backoff;
                    }
                }
            }

            return status;
        }

        bool ShouldRestart(IRuntimeModule module)
        {
            // we don't really know what status "Unknown" means
            if (module.RuntimeStatus == ModuleStatus.Unknown)
            {
                throw new ArgumentException("Module's runtime status is unknown which is not a valid status.");
            }

            if (module.RuntimeStatus == ModuleStatus.Backoff)
            {
                // compute how long we must wait before restarting this module
                TimeSpan coolOffPeriod = this.GetCoolOffPeriod(module.RestartCount);
                TimeSpan elapsedTime = DateTime.UtcNow - module.LastExitTimeUtc;

                // LastExitTime can be greater thatn UtcNow if the clock is off, so check if the elapsed time is > 0
                bool shouldRestart = elapsedTime > TimeSpan.Zero ? elapsedTime > coolOffPeriod : true;
                if (!shouldRestart)
                {
                    Events.ScheduledModule(module, elapsedTime, coolOffPeriod);
                }

                return shouldRestart;
            }

            return false;
        }

        internal TimeSpan GetCoolOffPeriod(int restartCount) =>
            TimeSpan.FromSeconds(Math.Min(this.coolOffTimeUnitInSeconds * Math.Pow(2, restartCount), MaxCoolOffPeriodSecs));

        public IEnumerable<IRuntimeModule> ApplyRestartPolicy(IEnumerable<IRuntimeModule> modules) =>
            modules.Where(module => this.ShouldRestart(module));
    }

    static class Events
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<RestartPolicyManager>();
        const int IdStart = AgentEventIds.RestartManager;

        enum EventIds
        {
            ScheduledModule = IdStart + 1
        }

        public static void ScheduledModule(IRuntimeModule module, TimeSpan elapsedTime, TimeSpan coolOffPeriod)
        {
            TimeSpan timeLeft = coolOffPeriod - elapsedTime;
            Log.LogInformation(
                (int)EventIds.ScheduledModule,
                $"Module '{module.Name}' scheduled to restart after {coolOffPeriod.Humanize()} ({timeLeft.Humanize()} left).");
        }
    }
}
