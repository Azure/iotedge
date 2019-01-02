// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    interface ITwinStore
    {
        Task<Option<Twin>> Get(string id);

        Task UpdateReportedProperties(string id, TwinCollection patch);

        Task UpdateDesiredProperties(string id, TwinCollection patch);

        Task Update(string id, Twin twin);
    }
}
