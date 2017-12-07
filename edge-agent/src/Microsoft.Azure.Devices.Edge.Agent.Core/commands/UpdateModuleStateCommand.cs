// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class UpdateModuleStateCommand : ICommand
    {
        readonly IModule module;
        readonly IEntityStore<string, ModuleState> store;
        readonly ModuleState state;

        public UpdateModuleStateCommand(IModule module, IEntityStore<string, ModuleState> store, ModuleState state)
        {
            this.module = Preconditions.CheckNotNull(module, nameof(module));
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.state = Preconditions.CheckNotNull(state, nameof(state));
        }

        public string Id => $"UpdateModuleStateCommand({this.module.Name}, [{this.state.RestartCount}, {this.state.LastRestartTimeUtc.ToString("o")}])";

        public Task ExecuteAsync(CancellationToken token) => this.store.PutOrUpdate(this.module.Name, this.state, _ => this.state);

        public string Show() => $"Update health stats for module {this.module.Name}";

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public override string ToString() => this.Show();
    }
}
