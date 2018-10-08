// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;

    public interface ITwinManager
    {
        Task<IMessage> GetTwinAsync(string id);

        Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection);

        Task UpdateReportedPropertiesAsync(string id, IMessage twinCollection);
    }
}
