// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;

    public class WeeklyAvailability
    {
        private readonly ISystemTime time;

        public string Name;
        public string Version;

        private Availability[] dailyAvaliabilities;
        private DayOfWeek currentDay;

        public WeeklyAvailability(string name, string version, ISystemTime time)
        {
            this.Name = name;
            this.Version = version;
            this.time = time;

            this.currentDay = time.UtcNow.DayOfWeek;
            this.dailyAvaliabilities = Enumerable.Range(0, 7).Select(_ => new Availability(name, version, time)).ToArray();
        }

        public WeeklyAvailability(WeeklyAvailabilityRaw raw, ISystemTime time)
        {
            this.Name = raw.Name;
            this.Version = raw.Version;
            this.time = time;

            this.currentDay = time.UtcNow.DayOfWeek;
            this.dailyAvaliabilities = raw.DailyAvaliabilities.Select(aRaw => new Availability(aRaw, time)).ToArray();

            if (time.UtcNow.DayOfWeek != raw.SavedDate.DayOfWeek)
            {
                int difference = (int)(time.UtcNow.Date - raw.SavedDate).Days;
                if (difference < 7)
                {
                    this.dailyAvaliabilities = this.dailyAvaliabilities.Take(7 - difference).Concat(Enumerable.Range(0, difference).Select(_ => new Availability(raw.Name, raw.Version, time))).ToArray();
                }
                else
                {
                    this.dailyAvaliabilities = Enumerable.Range(0, 7).Select(_ => new Availability(raw.Name, raw.Version, time)).ToArray();
                }
            }

            Debug.Assert(this.dailyAvaliabilities.Length == 7);
        }

        public double AvailabilityRatio
        {
            get
            {
                TimeSpan uptime = TimeSpan.Zero;
                TimeSpan totalTime = TimeSpan.Zero;
                foreach (Availability day in this.dailyAvaliabilities)
                {
                    uptime += day.Uptime;
                    totalTime += day.TotalTime;
                }

                if (totalTime == TimeSpan.Zero)
                {
                    return 0;
                }

                return uptime.TotalMilliseconds / totalTime.TotalMilliseconds;
            }
        }

        public void AddPoint(bool isUp)
        {
            this.CheckSwapover();
            this.dailyAvaliabilities[0].AddPoint(isUp);
        }

        public void NoPoint()
        {
            this.CheckSwapover();
            this.dailyAvaliabilities[0].NoPoint();
        }

        private void CheckSwapover()
        {
            if (this.time.UtcNow.DayOfWeek != this.currentDay)
            {
                /* note this doesn't need to be efficient since it only happens once a day */
                this.currentDay = this.time.UtcNow.DayOfWeek;
                Availability newDay = new Availability(this.Name, this.Version, this.time);
                List<Availability> newAvaliabilities = new List<Availability> { newDay };
                newAvaliabilities.AddRange(this.dailyAvaliabilities.Take(6));

                this.dailyAvaliabilities = newAvaliabilities.ToArray();
                Debug.Assert(this.dailyAvaliabilities.Length == 7);
            }
        }

        public WeeklyAvailabilityRaw ToRaw()
        {
            List<TimeSpan> uptimes = new List<TimeSpan>();
            List<TimeSpan> totalTimes = new List<TimeSpan>();
            foreach (Availability day in this.dailyAvaliabilities)
            {
                uptimes.Add(day.Uptime);
                totalTimes.Add(day.TotalTime);
            }

            return new WeeklyAvailabilityRaw
            {
                Name = this.Name,
                Version = this.Version,
                DailyAvaliabilities = this.dailyAvaliabilities.Select(a => a.ToRaw()).ToArray(),
                SavedDate = this.time.UtcNow.Date,
            };
        }
    }

    public struct WeeklyAvailabilityRaw
    {
        public string Name;
        public string Version;
        public AvailabilityRaw[] DailyAvaliabilities;
        public DateTime SavedDate;
    }
}
