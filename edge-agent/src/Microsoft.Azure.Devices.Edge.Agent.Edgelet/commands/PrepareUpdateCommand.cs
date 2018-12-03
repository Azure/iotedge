// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Devices.Edge.Agent.Core;

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
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

        public string Show() => $"Prepare update module {this.module.Name}";

        public string Id => $"PrepareUpdateCommand({this.module.Name})";

        public Task ExecuteAsync(CancellationToken token)
        {
            return this.moduleManager.PrepareUpdateAsync(this.module);
        }

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        static ModuleSpec BuildModuleSpec(IModule module, object settings)
        {
            var moduleSpec = new ModuleSpec
            {
                Name = module.Name,
                Config = new Config
                {
                    Settings = settings,
                    Env = new ObservableCollection<EnvVar>()
                },
                Type = module.Type
            };
            return moduleSpec;
        }
    }
}
