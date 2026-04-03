// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    interface ICloudSync
    {
        Task<Option<TwinProperties>> GetTwin(string id);

        Task<bool> UpdateReportedProperties(string id, PropertyCollection patch);
    }
}
