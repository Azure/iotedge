// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PrepareUpdateCommand : ICommand
    {
        readonly IModuleManager moduleManager;
        readonly ModuleSpec module;

        public PrepareUpdateCommand(IModuleManager moduleManager, IModule module, object settings)
        {
            this.moduleManager = moduleManager;
            this.module = BuildModuleSpec(module, settings);
        }

        public string Id => $"PrepareUpdateCommand({this.module.Name})";

        public string Show() => $"Prepare update module {this.module.Name}";

        public Task ExecuteAsync(CancellationToken token)
        {
            return this.moduleManager.PrepareUpdateAsync(this.module);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        static ModuleSpec BuildModuleSpec(IModule module, object settings)
        {
            return new ModuleSpec(module.Name, module.Type, module.ImagePullPolicy, settings);
        }
    }
}
