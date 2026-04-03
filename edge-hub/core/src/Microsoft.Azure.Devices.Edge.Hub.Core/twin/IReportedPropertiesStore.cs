// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    interface IReportedPropertiesStore
    {
        Task Update(string id, PropertyCollection patch);

        void InitSyncToCloud(string id);

        Task SyncToCloud(string id);
    }
}
