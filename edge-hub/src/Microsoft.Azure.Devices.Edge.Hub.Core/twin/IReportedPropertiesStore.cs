// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Shared;

    interface IReportedPropertiesStore
    {
        Task Update(string id, TwinCollection patch);

        void InitSyncToCloud(string id);

        Task SyncToCloud(string id);
    }
}
