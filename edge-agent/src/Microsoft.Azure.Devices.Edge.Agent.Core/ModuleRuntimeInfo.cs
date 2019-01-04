// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleRuntimeInfo
    {
        public ModuleRuntimeInfo(
            string name,
            string type,
            ModuleStatus moduleStatus,
            string description,
            long exitCode,
            Option<DateTime> startTime,
            Option<DateTime> exitTime)
        {
            this.Name = name;
            this.Type = type;
            this.ModuleStatus = moduleStatus;
            this.Description = description;
            this.ExitCode = exitCode;
            this.StartTime = startTime;
            this.ExitTime = exitTime;
        }

        public string Name { get; }

        public string Type { get; }

        public ModuleStatus ModuleStatus { get; }

        public string Description { get; }

        public long ExitCode { get; }

        public Option<DateTime> StartTime { get; }

        public Option<DateTime> ExitTime { get; }
    }

    public class ModuleRuntimeInfo<T> : ModuleRuntimeInfo
    {
        public ModuleRuntimeInfo(
            string name,
            string type,
            ModuleStatus moduleStatus,
            string description,
            long exitCode,
            Option<DateTime> startTime,
            Option<DateTime> exitTime,
            T config)
            : base(name, type, moduleStatus, description, exitCode, startTime, exitTime)
        {
            this.Config = config;
        }

        public T Config { get; }
    }
}
