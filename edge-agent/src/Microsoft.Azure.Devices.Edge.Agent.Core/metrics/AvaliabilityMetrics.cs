// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;

    static class AvailabilityMetrics
    {
        private static readonly IMetricsGauge LifetimeAvailability = Util.Metrics.Metrics.Instance.CreateGauge(
            "lifetime_availability",
            "total availability since deployment",
            new List<string> { "module_name", "module_version" });

        private static readonly IMetricsGauge WeeklyAvailability = Util.Metrics.Metrics.Instance.CreateGauge(
            "weekly_availability",
            "total availability for the last 7 days",
            new List<string> { "module_name", "module_version" });

        public static readonly ILogger Log = Logger.Factory.CreateLogger<Availability>();
        public static ISystemTime Time = SystemTime.Instance;
        public static Option<string> StoragePath = Option.None<string>();

        private static Lazy<List<(Availability lifetime, WeeklyAvailability weekly)>> availabilities = new Lazy<List<(Availability lifetime, WeeklyAvailability weekly)>>(LoadData);

        static AvailabilityMetrics()
        {
            AppDomain.CurrentDomain.ProcessExit += SaveData;
        }

        public static void ComputeAvailability(ModuleSet desired, ModuleSet current)
        {
            /* Get all modules that are not running but should be */
            var down = new HashSet<string>(current.Modules.Values
                .Where(c =>
                    (c is IRuntimeModule) &&
                    (c as IRuntimeModule).RuntimeStatus != ModuleStatus.Running &&
                    desired.Modules.TryGetValue(c.Name, out var d) &&
                    d.DesiredStatus == ModuleStatus.Running)
                .Select(c => c.Name));

            /* Get all correctly running modules */
            var up = new HashSet<string>(current.Modules.Values
                .Where(c => (c is IRuntimeModule) && (c as IRuntimeModule).RuntimeStatus == ModuleStatus.Running)
                .Select(c => c.Name));

            /* Add points for all modules found */
            foreach ((Availability lifetime, WeeklyAvailability weekly) in availabilities.Value)
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

                LifetimeAvailability.Set(lifetime.AvailabilityRatio, new[] { lifetime.Name, lifetime.Version });
                WeeklyAvailability.Set(weekly.AvailabilityRatio, new[] { weekly.Name, weekly.Version });
            }

            /* Add new modules to track */
            foreach (string module in down.Union(up))
            {
                availabilities.Value.Add((new Availability(module, "tempNoVersion", Time), new WeeklyAvailability(module, "tempNoVersion", Time)));
            }
        }

        private static List<(Availability lifetime, WeeklyAvailability weekly)> LoadData()
        {
            if (StoragePath.HasValue)
            {
                Log.LogInformation("Loading historical availability");

                List<Availability> lifetimeAvailabilities = new List<Availability>();
                string file = Path.Combine(StoragePath.OrDefault(), "AvailabilityHistory", "lifetime.json");
                if (File.Exists(file))
                {
                    try
                    {
                        lifetimeAvailabilities = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AvailabilityRaw>>(File.ReadAllText(file))
                            .Select(raw => new Availability(raw, Time)).ToList();
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"Could not load lifetime availability: {ex}");
                    }
                }

                List<WeeklyAvailability> weeklyAvailabilities = new List<WeeklyAvailability>();
                file = Path.Combine(StoragePath.OrDefault(), "AvailabilityHistory", "weekly.json");
                if (File.Exists(file))
                {
                    try
                    {
                        weeklyAvailabilities = Newtonsoft.Json.JsonConvert.DeserializeObject<List<WeeklyAvailabilityRaw>>(File.ReadAllText(file))
                            .Select(raw => new WeeklyAvailability(raw, Time)).ToList();
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"Could not load weekly availability: {ex}");
                    }
                }

                return lifetimeAvailabilities.Select(lifetimeAvailability =>
                {
                    /* don't care about efficienct since only happens once */
                    WeeklyAvailability weeklyAvailability = weeklyAvailabilities.Find(a => a.Name == lifetimeAvailability.Name) ?? new WeeklyAvailability(lifetimeAvailability.Name, lifetimeAvailability.Version, Time);
                    return (lifetimeAvailability, weeklyAvailability);
                }).ToList();
            }

            return new List<(Availability lifetime, WeeklyAvailability weekly)>();
        }

        private static void SaveData(object sender, EventArgs e)
        {
            if (StoragePath.HasValue)
            {
                Log.LogInformation("Saving avaliability data");
                try
                {
                    Directory.CreateDirectory(Path.Combine(StoragePath.OrDefault(), "AvailabilityHistory"));

                    File.WriteAllText(
                        Path.Combine(StoragePath.OrDefault(), "AvailabilityHistory", "lifetime.json"),
                        Newtonsoft.Json.JsonConvert.SerializeObject(availabilities.Value.Select(a => a.lifetime.ToRaw())));

                    File.WriteAllText(
                        Path.Combine(StoragePath.OrDefault(), "AvailabilityHistory", "weekly.json"),
                        Newtonsoft.Json.JsonConvert.SerializeObject(availabilities.Value.Select(a => a.weekly.ToRaw())));
                }
                catch (Exception ex)
                {
                    Log.LogError($"Could not save historical availability: {ex}");
                }
            }
        }
    }
}
