namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    public class SimpleCloudConnection : ICloudConnection
    {
        private ICloudProxy cloudProxy;

        public SimpleCloudConnection()
        {
            this.cloudProxy = new AllGoodCloudProxy();
        }

        public SimpleCloudConnection(ICloudProxy cloudProxy)
        {
            this.cloudProxy = cloudProxy;
        }

        public Option<ICloudProxy> CloudProxy => Option.Some(this.cloudProxy);

        public bool IsActive => true;
        public Task<bool> CloseAsync() => Task.FromResult(true);
    }
}
