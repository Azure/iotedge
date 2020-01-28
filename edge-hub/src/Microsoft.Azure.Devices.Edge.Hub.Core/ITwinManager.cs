// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ITwinManager
    {
        Task<IMessage> GetTwinAsync(string id);

        Task<Option<IMessage>> GetCachedTwinAsync(string id);

        Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection);

        Task UpdateReportedPropertiesAsync(string id, IMessage twinCollection);
    }
}
