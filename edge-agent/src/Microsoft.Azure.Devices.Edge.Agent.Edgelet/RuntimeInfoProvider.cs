// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json.Linq;

    public class RuntimeInfoProvider<T> : IRuntimeInfoProvider
    {
        readonly IModuleManager moduleManager;

        public RuntimeInfoProvider(IModuleManager moduleManager)
        {
            this.moduleManager = moduleManager;
        }

        public async Task<IEnumerable<ModuleRuntimeInfo>> GetModules(CancellationToken token)
        {
            IEnumerable<ModuleDetails> modules = await this.moduleManager.GetModules(token);
            IEnumerable<ModuleRuntimeInfo> modulesRuntimeInfo = modules.Select(m => GetModuleRuntimeInfo(m));
            return modulesRuntimeInfo;
        }

        public async Task<Core.SystemInfo> GetSystemInfo()
        {
            GeneratedCode.SystemInfo systemInfo = await this.moduleManager.GetSystemInfoAsync();

            return new Core.SystemInfo(systemInfo.OsType, systemInfo.Architecture);
        } 

        internal static ModuleRuntimeInfo<T> GetModuleRuntimeInfo(ModuleDetails moduleDetails)
        {
            ExitStatus exitStatus = moduleDetails.Status.ExitStatus;
            if (exitStatus == null || !long.TryParse(exitStatus.StatusCode, out long exitCode))
            {
                exitCode = 0;
            }

            Option<DateTime> exitTime = exitStatus == null ? Option.None<DateTime>() : Option.Some(exitStatus.ExitTime);
            Option<DateTime> startTime = !moduleDetails.Status.StartTime.HasValue ? Option.None<DateTime>() : Option.Some(moduleDetails.Status.StartTime.Value);

            if (!Enum.TryParse(moduleDetails.Status.RuntimeStatus.Status, true,  out ModuleStatus status))
            {
                status = ModuleStatus.Unknown;
            }

            if (!(moduleDetails.Config.Settings is JObject jobject))
            {
                throw new InvalidOperationException($"Module config is of type {moduleDetails.Config.Settings.GetType()}. Expected type JObject");
            }
            var config = jobject.ToObject<T>();

            var moduleRuntimeInfo = new ModuleRuntimeInfo<T>(moduleDetails.Name, moduleDetails.Type, status,
                moduleDetails.Status.RuntimeStatus.Description, exitCode, startTime, exitTime, config);
            return moduleRuntimeInfo;
        }
    }
}
