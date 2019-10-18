namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class SimpleCloudConnectionProvider : ICloudConnectionProvider
    {
        public void BindEdgeHub(IEdgeHub edgeHub)
        {
        }

        public Task<Try<ICloudConnection>> Connect(IClientCredentials clientCredentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler) => Task.FromResult(Try.Success(new SimpleCloudConnection() as ICloudConnection));
        public Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler) => Task.FromResult(Try.Success(new SimpleCloudConnection() as ICloudConnection));
    }
}
