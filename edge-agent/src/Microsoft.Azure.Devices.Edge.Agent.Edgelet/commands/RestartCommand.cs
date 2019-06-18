// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RestartCommand : ICommand
    {
        readonly string id;
        readonly IModuleManager moduleManager;

        public RestartCommand(IModuleManager moduleManager, string id)
        {
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
        }

        public string Id => $"RestartCommand({this.id})";

        public Task ExecuteAsync(CancellationToken token) => this.moduleManager.RestartModuleAsync(this.id);

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => $"Restart module {this.id}";
    }
}
