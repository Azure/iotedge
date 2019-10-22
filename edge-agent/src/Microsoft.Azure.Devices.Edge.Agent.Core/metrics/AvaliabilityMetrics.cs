// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    static class AvaliabilityMetrics
    {
        private static readonly IMetricsGauge LifetimeAvaliability = Util.Metrics.Metrics.Instance.CreateGauge(
            "lifetime_avaliability",
            "total availability since deployment",
            new List<string> { "module_name", "module_version" });

        private static readonly IMetricsGauge WeeklyAvaliability = Util.Metrics.Metrics.Instance.CreateGauge(
            "weekly_avaliability",
            "total availability for the last 7 days",
            new List<string> { "module_name", "module_version" });

        public static ISystemTime Time = SystemTime.Instance;
        public static Option<string> StoragePath = Option.None<string>();

        private static Lazy<List<(Avaliability lifetime, WeeklyAvaliability weekly)>> availabilities = new Lazy<List<(Avaliability lifetime, WeeklyAvaliability weekly)>>(LoadData);

        static AvaliabilityMetrics()
        {
            AppDomain.CurrentDomain.ProcessExit += SaveData;
        }

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
            foreach ((Avaliability lifetime, WeeklyAvaliability weekly) in availabilities.Value)
            {
                string name = lifetime.Name;
                if (down.Remove(name))
                {
                    lifetime.AddPoint(false);
                    weekly.AddPoint(false);
                }
                else if (up.Remove(name))
                {
                    lifetime.AddPoint(true);
                    weekly.AddPoint(true);
                }
                else
                {
                    /* stop calculating if in stopped state or not deployed */
                    lifetime.NoPoint();
                    weekly.NoPoint();
                }

                LifetimeAvaliability.Set(lifetime.AvaliabilityRatio, new[] { lifetime.Name, lifetime.Version });
                WeeklyAvaliability.Set(weekly.AvaliabilityRatio, new[] { weekly.Name, weekly.Version });
            }

            /* Add new modules to track */
            foreach (string module in down.Union(up))
            {
                availabilities.Value.Add((new Avaliability(module, "tempNoVersion", Time), new WeeklyAvaliability(module, "tempNoVersion", Time)));
            }
        }

        private static List<(Avaliability lifetime, WeeklyAvaliability weekly)> LoadData()
        {
            if (StoragePath.HasValue)
            {
                Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Loading historical avaliability");

                List<Avaliability> lifetimeAvailabilities = new List<Avaliability>();
                string file = Path.Combine(StoragePath.ToEnumerable().First(), "AvaliabilityHistory", "lifetime.json");
                if (File.Exists(file))
                {
                    try
                    {
                        lifetimeAvailabilities = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AvaliabilityRaw>>(File.ReadAllText(file))
                            .Select(raw => new Avaliability(raw, Time)).ToList();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Could not load lifetime avaliability: {ex}");
                    }
                }

                List<WeeklyAvaliability> weeklyAvailabilities = new List<WeeklyAvaliability>();
                file = Path.Combine(StoragePath.ToEnumerable().First(), "AvaliabilityHistory", "weekly.json");
                if (File.Exists(file))
                {
                    try
                    {
                        weeklyAvailabilities = Newtonsoft.Json.JsonConvert.DeserializeObject<List<WeeklyAvaliabilityRaw>>(File.ReadAllText(file))
                            .Select(raw => new WeeklyAvaliability(raw, Time)).ToList();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Could not load weekly avaliability: {ex}");
                    }
                }

                return lifetimeAvailabilities.Select(lifetimeAvaliability =>
                {
                    /* don't care about efficienct since only happens once */
                    WeeklyAvaliability weeklyAvailability = weeklyAvailabilities.Find(a => a.Name == lifetimeAvaliability.Name) ?? new WeeklyAvaliability(lifetimeAvaliability.Name, lifetimeAvaliability.Version, Time);
                    return (lifetimeAvaliability, weeklyAvailability);
                }).ToList();
            }

            return new List<(Avaliability lifetime, WeeklyAvaliability weekly)>();
        }

        private static void SaveData(object sender, EventArgs e)
        {
            if (StoragePath.HasValue)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(StoragePath.ToEnumerable().First(), "AvaliabilityHistory"));

                    File.WriteAllText(
                        Path.Combine(StoragePath.ToEnumerable().First(), "AvaliabilityHistory", "lifetime.json"),
                        Newtonsoft.Json.JsonConvert.SerializeObject(availabilities.Value.Select(a => a.lifetime.ToRaw())));

                    File.WriteAllText(
                        Path.Combine(StoragePath.ToEnumerable().First(), "AvaliabilityHistory", "weekly.json"),
                        Newtonsoft.Json.JsonConvert.SerializeObject(availabilities.Value.Select(a => a.weekly.ToRaw())));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.UtcNow.ToLogString()} Could not save historical avaliability: {ex}");
                }
            }
        }
    }
}
