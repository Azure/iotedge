// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    static class AvaliabilityMetrics
    {
        private static readonly IMetricsGauge LifetimeAvaliability = Util.Metrics.Metrics.Instance.CreateGauge(
            "lifetime_avaliability",
            "total availability since deployment",
            new List<string> { "module_name", "module_version" });

        public static ISystemTime Time = SystemTime.Instance;
        public static Option<string> StoragePath = Option.None<string>();
        private static Option<string> StorageFile
        {
            get { return StoragePath.Map(p => Path.Combine(p, "avaliability_history")); }
        }

        private static Lazy<List<Avaliability>> availabilities = new Lazy<List<Avaliability>>(() =>
        {
            if (StorageFile.HasValue)
            {
                Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Loading historical avaliability");
                string file = StorageFile.ToEnumerable().First();
                if (File.Exists(file))
                {
                    try
                    {
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<List<AvaliabilityRaw>>(File.ReadAllText(file))
                            .Select(raw => new Avaliability(raw, Time)).ToList();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Could not load historical avaliability: {ex}");
                    }
                }
            }

            return new List<Avaliability>();
        });

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
            foreach (Avaliability avaliability in availabilities.Value)
            {
                if (down.Remove(avaliability.Name))
                {
                    avaliability.AddPoint(false);
                }
                else if (up.Remove(avaliability.Name))
                {
                    avaliability.AddPoint(true);
                }
                else
                {
                    /* stop calculating if in stopped state or not deployed */
                    avaliability.NoPoint();
                }

                LifetimeAvaliability.Set(avaliability.AvaliabilityRatio, new[] { avaliability.Name, avaliability.Version });
            }

            /* Add new modules to track */
            foreach (string module in down.Union(up))
            {
                availabilities.Value.Add(new Avaliability(module, "tempNoVersion", Time));
            }

            if (StorageFile.HasValue)
            {
                try
                {
                    string file = StorageFile.ToEnumerable().First();
                    string data = Newtonsoft.Json.JsonConvert.SerializeObject(availabilities.Value.Select(av => av.ToRaw()));
                    File.WriteAllText(file, data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Could not save historical avaliability: {ex}");
                }
            }
        }
    }
}
