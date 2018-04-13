// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StopCommand : ICommand
    {
        static readonly TimeSpan WaitBeforeKill = TimeSpan.FromSeconds(10);
        readonly IModule module;
        readonly IModuleManager moduleManager;

        public StopCommand(IModuleManager moduleManager, IModule module)
        {
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public string Id => $"StopCommand({this.module.Name})";

        public Task ExecuteAsync(CancellationToken token) => this.moduleManager.StopModuleAsync(this.module.Name);

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"Stop module {this.module.Name}";
    }
}
