// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RemoveCommand : ICommand
    {
        readonly IModule module;
        readonly IModuleManager moduleManager;

        public RemoveCommand(IModuleManager moduleManager, IModule module)
        {
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public string Id => $"RemoveCommand({this.module.Name})";

        public Task ExecuteAsync(CancellationToken token) => this.moduleManager.DeleteModuleAsync(this.module.Name);

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"Remove module {this.module.Name}";
    }
}
