// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ITwinStore
    {
        Task<Option<TwinProperties>> Get(string id);

        Task UpdateReportedProperties(string id, PropertyCollection patch);

        Task UpdateDesiredProperties(string id, PropertyCollection patch);

        Task Update(string id, TwinProperties twin);
    }
}
