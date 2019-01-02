// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    interface ICloudSync
    {
        Task<Option<Twin>> GetTwin(string id);

        Task<bool> UpdateReportedProperties(string id, TwinCollection patch);
    }
}
