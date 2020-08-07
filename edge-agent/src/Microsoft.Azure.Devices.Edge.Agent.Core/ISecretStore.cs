using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public interface ISecretStore
    {
        Task<string> GetSecretAsync(string moduleName, string secretId);

        Task SetSecretAsync(string moduleName, string secretId, string secretValue);

        Task DeleteSecretAsync(string moduleName, string secretId);

        Task PullSecretAsync(string moduleName, string secretId, string akvId);

        Task RefreshSecretAsync(string moduleName, string secretId);
    }
}
