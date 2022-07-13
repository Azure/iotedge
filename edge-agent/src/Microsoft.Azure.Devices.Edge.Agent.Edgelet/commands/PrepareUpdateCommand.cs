// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.PlanRunner;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PrepareUpdateCommand : ICommand
    {
        readonly IModuleManager moduleManager;
        readonly ModuleSpec module;
        readonly ModuleUpdateMode moduleUpdateMode;

        public PrepareUpdateCommand(IModuleManager moduleManager, IModule module, object settings, ModuleUpdateMode moduleUpdateMode)
        {
            this.moduleManager = moduleManager;
            this.module = BuildModuleSpec(module, settings);
            this.moduleUpdateMode = moduleUpdateMode;
        }

        public string Id => $"PrepareUpdateCommand({this.module.Name})";

        public string Show() => $"Prepare module {this.module.Name}";

        public async Task ExecuteAsync(CancellationToken token)
        {
            try
            {
                await this.moduleManager.PrepareUpdateAsync(this.module);
            }
            catch (Exception e) when (this.moduleUpdateMode == ModuleUpdateMode.WaitForAll)
            {
                throw new ExcecutionPrerequisiteException("Failed to execute PrepareForUpdate command", e);
            }
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        static ModuleSpec BuildModuleSpec(IModule module, object settings)
        {
            return new ModuleSpec(module.Name, module.Type, module.ImagePullPolicy, settings);
        }
    }
}
