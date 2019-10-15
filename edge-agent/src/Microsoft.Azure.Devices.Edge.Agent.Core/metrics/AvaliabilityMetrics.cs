using Microsoft.Azure.Devices.Edge.Util.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    static class AvaliabilityMetrics
    {
        static List<Avaliability> avalabilities = new List<Avaliability>();

        static readonly IMetricsGauge LifetimeAvaliability = Util.Metrics.Metrics.Instance.CreateGauge(
            "lifetime_avaliability",
            "total availability since deployment",
            new List<string> { "module_name", "module_version" }
        );


        public static void ComputeAvaliability(ModuleSet desired, ModuleSet current)
        {
            /* Get all modules that are not running but should be */
            var down = new HashSet<string>(current.Modules.Values
                .Where(c =>
                    (c as IRuntimeModule).RuntimeStatus != ModuleStatus.Running &&
                    desired.Modules.TryGetValue(c.Name, out var d) &&
                    d.DesiredStatus == ModuleStatus.Running)
                .Select(c => c.Name));

            /* Get all correctly running modules */
            var up = new HashSet<string>(current.Modules.Values
                .Where(c => (c as IRuntimeModule).RuntimeStatus == ModuleStatus.Running)
                .Select(c => c.Name));

            /* Add points for all modules found */
            foreach (Avaliability avaliability in avalabilities)
            {
                if (down.Remove(avaliability.name))
                {
                    avaliability.AddPoint(false);
                }
                else if (up.Remove(avaliability.name))
                {
                    avaliability.AddPoint(true);
                }
                else
                {
                    /* stop calculating if in stopped state or not deployed */
                    avaliability.NoPoint();
                }


                // TODO: make set take double
                LifetimeAvaliability.Set((long)(avaliability.avaliability * 10000), new[] { avaliability.name, avaliability.version });
            }

            /* Add new modules to track */
            foreach (string module in down.Union(up))
            {
                avalabilities.Add(new Avaliability(module, "tempNoVersion"));
            }

        }
    }
}
