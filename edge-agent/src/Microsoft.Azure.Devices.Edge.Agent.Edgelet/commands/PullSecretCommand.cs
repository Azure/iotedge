using Microsoft.Azure.Devices.Edge.Agent.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands
{
    public class PullSecretCommand : ICommand
    {
        readonly IModule module;
        readonly ISecretManager secretManager;
        readonly string secretId;
        readonly string akvId;

        public PullSecretCommand(ISecretManager secretManager, IModule module, string secretId, string akvId)
        {
            this.secretManager = secretManager;
            this.module = module;
            this.secretId = secretId;
            this.akvId = akvId;
        }

        public string Id => $"PullSecret({this.module.Name}, {this.secretId})";

        public Task ExecuteAsync(CancellationToken token) => this.secretManager.PullSecretAsync(this.module.Name, this.secretId, this.akvId);

        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;

        public string Show() => $"Pull secret {this.secretId} for module {this.module.Name} from ${this.akvId}";
    }
}
