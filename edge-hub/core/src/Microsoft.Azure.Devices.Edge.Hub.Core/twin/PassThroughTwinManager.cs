// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;

    public class PassThroughTwinManager : ITwinManager
    {
        readonly IConnectionManager connectionManager;
        readonly IMessageConverter<TwinProperties> twinConverter;

        public PassThroughTwinManager(IConnectionManager connectionManager, IMessageConverterProvider messageConverterProvider)
        {
            Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinConverter = messageConverterProvider.Get<TwinProperties>();
        }

        public async Task<IMessage> GetTwinAsync(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
            IMessage twin = await cloudProxy
                .Map(c => c.GetTwinAsync())
                .GetOrElse(() => Task.FromResult(this.twinConverter.ToMessage(new TwinProperties())));
            return twin;
        }

        public Task<Option<IMessage>> GetCachedTwinAsync(string id)
        {
            return Task.FromResult(Option.None<IMessage>());
        }

        public Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
            return deviceProxy.ForEachAsync(dp => dp.OnDesiredPropertyUpdates(twinCollection));
        }

        public async Task UpdateReportedPropertiesAsync(string id, IMessage twinCollection)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
            await cloudProxy.ForEachAsync(cp => cp.UpdateReportedPropertiesAsync(twinCollection));
        }
    }
}
