// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Threading.Tasks;

    public interface ICheckpointerFactory
    {
        Task<ICheckpointer> CreateAsync(string id);
    }
}
