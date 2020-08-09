using Microsoft.Azure.Devices.Edge.Agent.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    public class GetSecretCommand : ICommand
    {
        readonly IModule module;
        readonly ISecretManager secretManager;
        readonly string secretId;

        public GetSecretCommand(ISecretManager secretManager, IModule module, string secretId)
        {
            this.secretManager = secretManager;
            this.module = module;
            this.secretId = secretId;
        }

        public string Id => $"GetSecret({this.module.Name}, {this.secretId})";

        public Task ExecuteAsync(CancellationToken token) => this.secretManager.GetSecretAsync(this.module.Name, this.secretId);

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public string Show() => $"Get secret {this.secretId} for module {this.module.Name}";
    }
}
