// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RemoveModuleStateCommand : ICommand
    {
        readonly IModule module;
        readonly IEntityStore<string, ModuleState> store;

        public RemoveModuleStateCommand(IModule module, IEntityStore<string, ModuleState> store)
        {
            this.module = Preconditions.CheckNotNull(module, nameof(module));
            this.store = Preconditions.CheckNotNull(store, nameof(store));
        }

        public string Id => $"RemoveModuleStateCommand({this.module.Name})";

        public Task ExecuteAsync(CancellationToken token) => this.store.Remove(this.module.Name);

        public string Show() => $"Reset health stats for module {this.module.Name}";

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public override string ToString() => this.Show();
    }
}
